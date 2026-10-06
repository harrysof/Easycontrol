using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EasyControl.Models;
using Vortice.DirectInput;

namespace EasyControl.Services;

public sealed record DirectInputDevice(
    DeviceInstance Instance,
    bool IsXInput,
    int VendorId,
    int ProductId,
    bool IsVirtualOutput)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Instance.ProductName)
        ? "Unknown controller"
        : Instance.ProductName!;

    public string Id => Instance.InstanceGuid.ToString("N");

    public string HardwareId => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
}

/// <summary>
/// Enumerates and polls DirectInput game controllers. All device calls run on a
/// single dedicated thread so COM affinity is preserved.
/// </summary>
public sealed class DirectInputService : IDisposable
{
    private readonly object _gate = new();
    private ControllerState _state = new();
    private Thread? _thread;
    private volatile bool _stop;
    private Guid _instanceGuid;
    private IntPtr _hwnd;

    /// <summary>Raised on the polling thread for every new input frame.</summary>
    public event Action<ControllerState>? StateUpdated;

    /// <summary>Raised when the polling device is removed or fails.</summary>
    public event Action<Exception>? DeviceLost;

    /// <summary>Raised once with the device capability information after acquisition.</summary>
    public event Action<DeviceCapabilitiesInfo>? DeviceReady;

    public bool IsRunning => _thread is { IsAlive: true };

    public void SetMessageWindow(IntPtr hwnd) => _hwnd = hwnd;

    public IReadOnlyList<DirectInputDevice> Enumerate()
    {
        using var directInput = DInput.DirectInput8Create();
        var devices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);
        var result = new List<DirectInputDevice>(devices.Count);

        foreach (var device in devices)
        {
            var instanceName = device.InstanceName ?? string.Empty;
            var isXInput = instanceName.Contains("IG_", StringComparison.OrdinalIgnoreCase);
            var (vid, pid) = ControllerIds.VidPid(device.ProductGuid);
            var isVirtual = ControllerIds.IsVirtualOutput(device.ProductGuid);

            // Skip the virtual pads EasyControl (or another ViGEm client) creates —
            // they are output, not input, and mapping them causes a feedback loop.
            if (isVirtual)
            {
                continue;
            }

            result.Add(new DirectInputDevice(device, isXInput, vid, pid, isVirtual));
        }

        return result;
    }

    /// <summary>Reads the capabilities of a device without starting the polling loop.</summary>
    public DeviceCapabilitiesInfo GetCapabilities(DeviceInstance instance)
    {
        using var directInput = DInput.DirectInput8Create();
        using var device = directInput.CreateDevice(instance.InstanceGuid);
        device.SetDataFormat<RawJoystickState>();
        var capabilities = device.Capabilities;
        return new DeviceCapabilitiesInfo(
            Math.Clamp(capabilities.ButtonCount, 0, 128),
            capabilities.AxeCount,
            capabilities.PovCount);
    }

    public void Start(DeviceInstance instance)
    {
        Stop();

        _instanceGuid = instance.InstanceGuid;
        _stop = false;

        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "EasyControl.DirectInput"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        var thread = _thread;
        _thread = null;

        if (thread is not null && thread.IsAlive && thread != Thread.CurrentThread)
        {
            thread.Join(500);
        }
    }

    private void PollLoop()
    {
        IDirectInput8? directInput = null;
        IDirectInputDevice8? device = null;

        try
        {
            directInput = DInput.DirectInput8Create();
            device = directInput.CreateDevice(_instanceGuid);
            device.SetDataFormat<RawJoystickState>();

            var capabilities = device.Capabilities;
            var buttonCount = Math.Clamp(capabilities.ButtonCount, 0, 128);
            var info = new DeviceCapabilitiesInfo(buttonCount, capabilities.AxeCount, capabilities.PovCount);
            _state = new ControllerState { Buttons = new bool[Math.Max(buttonCount, 1)] };

            DeviceReady?.Invoke(info);

            if (_hwnd != IntPtr.Zero)
            {
                device.SetCooperativeLevel(_hwnd, CooperativeLevel.Background | CooperativeLevel.NonExclusive);
            }

            device.Acquire();

            while (!_stop)
            {
                device.Poll();
                var js = device.GetCurrentJoystickState();

                var state = _state;
                state.X = js.X;
                state.Y = js.Y;
                state.Z = js.Z;
                state.Rx = js.RotationX;
                state.Ry = js.RotationY;
                state.Rz = js.RotationZ;

                var buttons = js.Buttons;
                var buttonSpan = state.Buttons;
                var count = Math.Min(buttons.Length, buttonSpan.Length);
                for (var i = 0; i < count; i++)
                {
                    buttonSpan[i] = buttons[i];
                }

                var sliders = js.Sliders;
                state.Slider0 = sliders.Length > 0 ? sliders[0] : 0;
                state.Slider1 = sliders.Length > 1 ? sliders[1] : 0;

                var povs = js.PointOfViewControllers;
                state.Pov0 = povs.Length > 0 ? povs[0] : -1;

                state.IsConnected = true;
                StateUpdated?.Invoke(state);

                Thread.Sleep(8);
            }
        }
        catch (Exception ex)
        {
            if (!_stop)
            {
                DeviceLost?.Invoke(ex);
            }
        }
        finally
        {
            try
            {
                device?.Unacquire();
            }
            catch
            {
                // ignored
            }

            device?.Dispose();
            directInput?.Dispose();
        }
    }

    public void Dispose() => Stop();
}

public readonly record struct DeviceCapabilitiesInfo(int ButtonCount, int AxisCount, int PovCount)
{
    public bool HasZ => AxisCount >= 3;

    public bool HasRotation => AxisCount >= 5;
}
