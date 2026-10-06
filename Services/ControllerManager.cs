using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyControl.Models;

namespace EasyControl.Services;

/// <summary>What kind of physical input a capture is waiting for.</summary>
public enum CaptureMode
{
    Button,
    Axis,
    Any
}

/// <summary>
/// Ties the DirectInput reader, the mapping engine, the virtual pad and system
/// actions together. Runs the mapping loop on the DirectInput polling thread.
/// </summary>
public sealed class ControllerManager : IDisposable
{
    private readonly DirectInputService _directInput;
    private readonly VigemPadService _vigem;
    private readonly SettingsService _settings;
    private readonly HidHideService _hidHide;
    private readonly KeyboardHookService _keyboard;
    private readonly InputMappingEngine _engine = new();
    private readonly PadFrame _frame = new();
    private readonly SemaphoreSlim _hideGate = new(1, 1);

    private GameProfile? _profile;
    private DirectInputDevice? _device;
    private IReadOnlyList<string>? _hiddenInstanceIds;

    private bool[] _capturePrevious = [];
    private Action<string>? _captureCallback;
    private CaptureMode _captureMode = CaptureMode.Button;

    private bool[] _lastButtons = [];
    private int _lastX, _lastY, _lastZ, _lastRx, _lastRy, _lastRz, _lastS0, _lastS1;
    private int _baseX, _baseY, _baseZ, _baseRx, _baseRy, _baseRz, _baseS0, _baseS1;

    /// <summary>Raised on the polling thread for preview / capture consumers.</summary>
    public event Action<ControllerState>? StateSampled;

    /// <summary>Raised when the active device disappears.</summary>
    public event Action<Exception>? DeviceLost;

    /// <summary>Raised after physical-device hiding is applied (true = hidden).</summary>
    public event Action<bool>? HidingChanged;

    public ControllerManager(
        DirectInputService directInput,
        VigemPadService vigem,
        SettingsService settings,
        HidHideService hidHide,
        KeyboardHookService keyboard)
    {
        _directInput = directInput;
        _vigem = vigem;
        _settings = settings;
        _hidHide = hidHide;
        _keyboard = keyboard;
        _directInput.StateUpdated += OnStateUpdated;
        _directInput.DeviceLost += OnDeviceLost;
        _keyboard.ActionTriggered += OnKeyboardAction;
        _keyboard.Install();
    }

    public bool IsPolling => _directInput.IsRunning;

    public bool IsOutputConnected => _vigem.IsConnected;

    public int OutputUserIndex => _vigem.UserIndex;

    public static string OutputName => VigemPadService.PadName;

    public bool DriverAvailable => _vigem.DriverAvailable;

    public string? DriverMessage => _vigem.DriverMessage;

    public bool IsHidingActive => _hiddenInstanceIds is { Count: > 0 };

    public void SetProfile(GameProfile? profile)
    {
        _profile = profile;
        UpdateKeyboardBlocks();
    }

    private void UpdateKeyboardBlocks()
    {
        var map = new List<KeyValuePair<string, string>>();
        var profile = _profile;

        if (profile is not null)
        {
            if (KeyboardHookService.IsKeyboardSource(profile.ScreenshotSource))
            {
                map.Add(new(profile.ScreenshotSource!, ActionIds.Screenshot));
            }

            if (KeyboardHookService.IsKeyboardSource(profile.GameBarSource))
            {
                map.Add(new(profile.GameBarSource!, ActionIds.GameBar));
            }
        }

        _keyboard.SetRuntimeBlocks(map);
    }

    private void OnKeyboardAction(string action)
    {
        switch (action)
        {
            case ActionIds.Screenshot:
                SystemActions.TakeScreenshot();
                break;
            case ActionIds.GameBar:
                SystemActions.OpenGameBar();
                break;
        }
    }

    public string? ActiveDeviceId => _device?.Instance.InstanceGuid.ToString("N");

    public void StartDevice(DirectInputDevice device)
    {
        _device = device;
        _capturePrevious = [];
        _directInput.Start(device.Instance);
    }

    public void StopDevice()
    {
        ApplyHiding(false);
        _directInput.Stop();
        _device = null;
    }

    /// <summary>Initialises and reports ViGEmBus availability without connecting a pad.</summary>
    public bool EnableDriverProbe()
    {
        if (!_vigem.DriverAvailable)
        {
            _vigem.Initialize();
        }

        return _vigem.DriverAvailable;
    }

    public bool EnableOutput()
    {
        if (!_vigem.DriverAvailable)
        {
            _vigem.Initialize();
        }

        var position = _settings.Settings.VirtualControllerPosition;
        _vigem.PreferredUserIndex = position > 0 ? position - 1 : -1;

        var connected = _vigem.Connect();
        if (connected)
        {
            ApplyHiding(true);
        }

        return connected;
    }

    public void DisableOutput()
    {
        ApplyHiding(false);
        _vigem.Disconnect();
    }

    public void BeginCapture(Action<string> onCaptured) => BeginCapture(CaptureMode.Button, onCaptured);

    /// <summary>
    /// Captures the next input for a special action. Listens on both the game
    /// controller and the keyboard, so buttons that live on a non-gamepad HID
    /// interface (e.g. the Guide/Home key) can be bound too.
    /// </summary>
    public void BeginActionCapture(Action<string> onCaptured)
    {
        _keyboard.BeginCapture(spec =>
        {
            CancelCapture();
            onCaptured(spec);
        });

        BeginCapture(CaptureMode.Button, source =>
        {
            _keyboard.CancelCapture();
            onCaptured(source);
        });
    }

    public void BeginCapture(CaptureMode mode, Action<string> onCaptured)
    {
        _captureMode = mode;
        _capturePrevious = (bool[])_lastButtons.Clone();
        _baseX = _lastX;
        _baseY = _lastY;
        _baseZ = _lastZ;
        _baseRx = _lastRx;
        _baseRy = _lastRy;
        _baseRz = _lastRz;
        _baseS0 = _lastS0;
        _baseS1 = _lastS1;
        _captureCallback = onCaptured;
    }

    public void CancelCapture()
    {
        _captureCallback = null;
        _capturePrevious = [];
        _keyboard.CancelCapture();
    }

    private void ApplyHiding(bool hide)
    {
        if (!_settings.Settings.HidePhysical || !_hidHide.IsInstalled)
        {
            return;
        }

        var device = _device;
        if (device is null)
        {
            return;
        }

        Task.Run(() =>
        {
            _hideGate.Wait();
            try
            {
                if (hide)
                {
                    var ids = _hidHide.FindInstanceIds(device);
                    if (ids.Count == 0)
                    {
                        return;
                    }

                    _hiddenInstanceIds = ids;
                    _hidHide.Apply(true, ids);
                    HidingChanged?.Invoke(true);
                }
                else
                {
                    var ids = _hiddenInstanceIds;
                    if (ids is null)
                    {
                        return;
                    }

                    _hidHide.Apply(false, ids);
                    _hiddenInstanceIds = null;
                    HidingChanged?.Invoke(false);
                }
            }
            finally
            {
                _hideGate.Release();
            }
        });
    }

    private void OnStateUpdated(ControllerState state)
    {
        var callback = _captureCallback;

        var profile = _profile;
        if (profile is not null && _vigem.IsConnected)
        {
            _engine.Apply(profile, state, _frame);
            _vigem.Submit(_frame);

            if (_frame.Screenshot)
            {
                SystemActions.TakeScreenshot();
            }

            if (_frame.GameBar)
            {
                SystemActions.OpenGameBar();
            }
        }

        if (callback is not null)
        {
            DetectCapture(state, callback);
        }

        CacheState(state);

        StateSampled?.Invoke(state);
    }

    private void CacheState(ControllerState state)
    {
        var buttons = state.Buttons;
        if (_lastButtons.Length != buttons.Length)
        {
            _lastButtons = new bool[buttons.Length];
        }

        Array.Copy(buttons, _lastButtons, buttons.Length);
        _lastX = state.X;
        _lastY = state.Y;
        _lastZ = state.Z;
        _lastRx = state.Rx;
        _lastRy = state.Ry;
        _lastRz = state.Rz;
        _lastS0 = state.Slider0;
        _lastS1 = state.Slider1;
    }

    private void DetectCapture(ControllerState state, Action<string> callback)
    {
        if (_captureMode is CaptureMode.Axis or CaptureMode.Any
            && TryDetectAxis(state, out var axisSource))
        {
            CompleteCapture(axisSource, callback);
            return;
        }

        if (_captureMode is CaptureMode.Button or CaptureMode.Any)
        {
            DetectDigital(state, callback);
        }
    }

    private void DetectDigital(ControllerState state, Action<string> callback)
    {
        var buttons = state.Buttons;
        if (_capturePrevious.Length != buttons.Length)
        {
            _capturePrevious = new bool[buttons.Length];
        }

        for (var i = 0; i < buttons.Length && i < SourceCatalog.MaxButtons; i++)
        {
            if (buttons[i] && !_capturePrevious[i])
            {
                CompleteCapture($"B{i + 1}", callback);
                return;
            }

            _capturePrevious[i] = buttons[i];
        }

        if (state.Pov0 >= 0)
        {
            var source = state.Pov0 switch
            {
                var a when a is >= 31500 or <= 4500 => SourceCatalog.Pov0Up,
                var a when a is >= 4500 and <= 13500 => SourceCatalog.Pov0Right,
                var a when a is >= 13500 and <= 22500 => SourceCatalog.Pov0Down,
                _ => SourceCatalog.Pov0Left
            };
            CompleteCapture(source, callback);
        }
    }

    private bool TryDetectAxis(ControllerState state, out string source)
    {
        var best = 0f;
        var bestSource = string.Empty;

        Consider(SourceCatalog.AxisX, DeltaBipolar(state.X, _baseX));
        Consider(SourceCatalog.AxisY, DeltaBipolar(state.Y, _baseY));
        Consider(SourceCatalog.AxisZ, DeltaBipolar(state.Z, _baseZ));
        Consider(SourceCatalog.AxisRx, DeltaBipolar(state.Rx, _baseRx));
        Consider(SourceCatalog.AxisRy, DeltaBipolar(state.Ry, _baseRy));
        Consider(SourceCatalog.AxisRz, DeltaBipolar(state.Rz, _baseRz));
        Consider(SourceCatalog.Slider0, DeltaUnipolar(state.Slider0, _baseS0));
        Consider(SourceCatalog.Slider1, DeltaUnipolar(state.Slider1, _baseS1));

        source = bestSource;
        return best > 0.6f;

        void Consider(string key, float delta)
        {
            if (delta > best)
            {
                best = delta;
                bestSource = key;
            }
        }
    }

    private static float DeltaBipolar(int value, int baseline) =>
        MathF.Abs(NormalizeBipolar(value) - NormalizeBipolar(baseline));

    private static float DeltaUnipolar(int value, int baseline) =>
        MathF.Abs(value - baseline) / 65535f;

    private static float NormalizeBipolar(int value) =>
        Math.Clamp((value - 32767.5f) / 32767.5f, -1f, 1f);

    private void CompleteCapture(string source, Action<string> callback)
    {
        _captureCallback = null;
        _capturePrevious = [];
        callback(source);
    }

    private void OnDeviceLost(Exception exception)
    {
        _captureCallback = null;
        DeviceLost?.Invoke(exception);
    }

    public void Dispose()
    {
        _directInput.StateUpdated -= OnStateUpdated;
        _directInput.DeviceLost -= OnDeviceLost;
        _keyboard.ActionTriggered -= OnKeyboardAction;

        var ids = _hiddenInstanceIds;
        if (ids is not null)
        {
            _hidHide.Apply(false, ids);
            _hiddenInstanceIds = null;
        }

        _directInput.Dispose();
        _vigem.Dispose();
    }
}
