using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyControl.Models;
using EasyControl.Services;

namespace EasyControl.ViewModels;

public sealed partial class ProfileViewModel : ObservableObject
{
    private readonly GameProfile _profile;
    private readonly DirectInputDevice _device;
    private readonly ControllerManager _manager;
    private readonly ProfileService _profiles;
    private readonly DirectInputService _directInput;
    private readonly Action _onBack;

    public ProfileViewModel(
        GameProfile profile,
        DirectInputDevice device,
        DirectInputService directInput,
        ControllerManager manager,
        ProfileService profiles,
        Action onBack)
    {
        _profile = profile;
        _device = device;
        _directInput = directInput;
        _manager = manager;
        _profiles = profiles;
        _onBack = onBack;

        _name = profile.Name;
        _isDefault = profile.IsDefault;
        _screenshotLabel = KeyboardHookService.Label(profile.ScreenshotSource);
        _gameBarLabel = KeyboardHookService.Label(profile.GameBarSource);

        DeviceName = device.DisplayName;
        DeviceBadge = device.IsXInput ? "XInput" : "DirectInput";

        BuildRows();
        RefreshRows();
        Activate();
    }

    public string DeviceName { get; }

    public string DeviceBadge { get; }

    public ObservableCollection<TargetRowViewModel> ButtonRows { get; } = [];

    public ObservableCollection<TargetRowViewModel> TriggerRows { get; } = [];

    public ObservableCollection<TargetRowViewModel> StickRows { get; } = [];

    public string InputStatus => $"{DeviceBadge} input";

    public string OutputStatus
    {
        get
        {
            if (!_manager.DriverAvailable)
            {
                return _manager.DriverMessage ?? "ViGEmBus unavailable";
            }

            if (!_manager.IsOutputConnected)
            {
                return "Virtual controller offline";
            }

            var slot = _manager.OutputUserIndex;
            return slot >= 0
                ? $"{VigemPadService.PadName} \u2022 XInput slot {slot}"
                : $"{VigemPadService.PadName} \u2022 connected";
        }
    }

    public bool ControllerOk => _manager.DriverAvailable && _manager.IsOutputConnected;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private string _screenshotLabel;

    [ObservableProperty]
    private string _gameBarLabel;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private bool _isCapturingGameBar;

    [ObservableProperty]
    private bool _isMappingEnabled = true;

    [ObservableProperty]
    private TargetRowViewModel? _capturingRow;

    public bool IsListening => CapturingRow is not null;

    partial void OnNameChanged(string value)
    {
        _profile.Name = value;
        Save();
    }

    partial void OnIsDefaultChanged(bool value)
    {
        if (value)
        {
            _profiles.SetDefault(_profile);
        }
        else
        {
            _profile.IsDefault = false;
            Save();
        }
    }

    partial void OnIsMappingEnabledChanged(bool value)
    {
        if (value)
        {
            _manager.EnableOutput();
        }
        else
        {
            _manager.DisableOutput();
        }

        OnPropertyChanged(nameof(OutputStatus));
        OnPropertyChanged(nameof(ControllerOk));
    }

    partial void OnCapturingRowChanged(TargetRowViewModel? value)
    {
        OnPropertyChanged(nameof(IsListening));
    }

    private void BuildRows()
    {
        AddRows(TargetCatalog.Buttons, ButtonRows);
        AddRows(TargetCatalog.Triggers, TriggerRows);
        AddRows(TargetCatalog.Sticks, StickRows);
    }

    private void AddRows(IReadOnlyList<TargetDescriptor> descriptors, ObservableCollection<TargetRowViewModel> target)
    {
        foreach (var descriptor in descriptors)
        {
            target.Add(new TargetRowViewModel(descriptor, BeginAssign, ClearTarget, ToggleInvert));
        }
    }

    private void RefreshRows()
    {
        foreach (var row in AllRows())
        {
            row.Apply(_profile.SourceFor(row.Target), _profile.InvertFor(row.Target));
        }
    }

    private IEnumerable<TargetRowViewModel> AllRows()
    {
        foreach (var row in ButtonRows)
        {
            yield return row;
        }

        foreach (var row in TriggerRows)
        {
            yield return row;
        }

        foreach (var row in StickRows)
        {
            yield return row;
        }
    }

    private void Activate()
    {
        _manager.SetProfile(_profile);
        _manager.StartDevice(_device);
        _manager.EnableOutput();
        OnPropertyChanged(nameof(OutputStatus));
        OnPropertyChanged(nameof(ControllerOk));
    }

    private void BeginAssign(TargetRowViewModel row)
    {
        if (!_manager.IsPolling)
        {
            _manager.StartDevice(_device);
        }

        CancelCapture();

        var mode = row.Kind switch
        {
            TargetKind.Stick => CaptureMode.Axis,
            TargetKind.Trigger => CaptureMode.Any,
            _ => CaptureMode.Button
        };

        CapturingRow = row;
        row.IsCapturing = true;
        _manager.BeginCapture(mode, source => CompleteAssign(row, source));
    }

    private void CompleteAssign(TargetRowViewModel row, string source)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            row.IsCapturing = false;
            if (ReferenceEquals(CapturingRow, row))
            {
                CapturingRow = null;
            }

            _profile.Assign(source, row.Target);
            RefreshRows();
            Save();
        });
    }

    private void ClearTarget(TargetRowViewModel row)
    {
        if (ReferenceEquals(CapturingRow, row))
        {
            CancelCapture();
        }

        _profile.ClearTarget(row.Target);
        RefreshRows();
        Save();
    }

    private void ToggleInvert(TargetRowViewModel row)
    {
        _profile.SetInvert(row.Target, row.IsInverted);
        Save();
    }

    [RelayCommand]
    private void CancelAssign() => CancelCapture();

    private void CancelCapture()
    {
        _manager.CancelCapture();

        var row = CapturingRow;
        if (row is not null)
        {
            row.IsCapturing = false;
            CapturingRow = null;
        }
    }

    [RelayCommand]
    private void SetScreenshot()
    {
        if (!_manager.IsPolling)
        {
            _manager.StartDevice(_device);
        }

        CancelCapture();
        IsCapturing = true;
        _manager.BeginActionCapture(source =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return;
            }

            dispatcher.Invoke(() =>
            {
                _profile.ScreenshotSource = source;
                ScreenshotLabel = KeyboardHookService.Label(source);
                IsCapturing = false;
                Save();
            });
        });
    }

    [RelayCommand]
    private void ClearScreenshot()
    {
        _manager.CancelCapture();
        IsCapturing = false;
        _profile.ScreenshotSource = null;
        ScreenshotLabel = "Not set";
        Save();
    }

    [RelayCommand]
    private void SetGameBar()
    {
        if (!_manager.IsPolling)
        {
            _manager.StartDevice(_device);
        }

        CancelCapture();
        IsCapturingGameBar = true;
        _manager.BeginActionCapture(source =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return;
            }

            dispatcher.Invoke(() =>
            {
                _profile.GameBarSource = source;
                GameBarLabel = KeyboardHookService.Label(source);
                IsCapturingGameBar = false;
                Save();
            });
        });
    }

    [RelayCommand]
    private void ClearGameBar()
    {
        _manager.CancelCapture();
        IsCapturingGameBar = false;
        _profile.GameBarSource = null;
        GameBarLabel = "Not set";
        Save();
    }

    [RelayCommand]
    private void Back() => _onBack();

    private void Save() => _profiles.Save();

    public void Deactivate()
    {
        CancelCapture();
        _manager.CancelCapture();
        IsCapturing = false;
        _manager.StopDevice();
        _manager.DisableOutput();
        _manager.SetProfile(null);
    }
}
