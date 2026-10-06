using System;
using System.Collections.Generic;
using System.Threading;
using EasyControl.Interop;
using EasyControl.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace EasyControl.Services;

/// <summary>Creates and feeds a virtual Xbox 360 controller via ViGEmBus.</summary>
public sealed class VigemPadService : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _pad;

    /// <summary>
    /// Desired XInput player slot (0-3), or -1 to let ViGEmBus pick the lowest
    /// free one. Achieved by briefly occupying lower free slots with throwaway
    /// pads: XInput does not renumber a device after the ones below it go away.
    /// </summary>
    public int PreferredUserIndex { get; set; } = -1;

    public bool DriverAvailable { get; private set; }

    public string? DriverMessage { get; private set; }

    public bool IsConnected => _pad is not null;

    public const string PadName = "Xbox 360 Controller";

    /// <summary>XInput player slot reported by the virtual pad, or -1.</summary>
    public int UserIndex
    {
        get
        {
            try
            {
                return _pad?.UserIndex ?? -1;
            }
            catch
            {
                return -1;
            }
        }
    }

    public bool Initialize()
    {
        if (_client is not null)
        {
            return DriverAvailable;
        }

        try
        {
            _client = new ViGEmClient();
            DriverAvailable = true;
            DriverMessage = null;
        }
        catch (VigemBusNotFoundException)
        {
            _client = null;
            DriverAvailable = false;
            DriverMessage = "ViGEmBus is not installed. Install it to enable virtual controller output.";
        }
        catch (Exception ex)
        {
            _client = null;
            DriverAvailable = false;
            DriverMessage = ex.Message;
        }

        return DriverAvailable;
    }

    public bool Connect()
    {
        if (_pad is not null)
        {
            return true;
        }

        if (!Initialize())
        {
            return false;
        }

        var dummies = new List<IXbox360Controller>();

        try
        {
            if (PreferredUserIndex > 0)
            {
                PlaceAt(PreferredUserIndex, dummies);
            }

            _pad = _client!.CreateXbox360Controller();
            _pad.AutoSubmitReport = false;
            _pad.Connect();
            return true;
        }
        catch (Exception ex)
        {
            DriverMessage = ex.Message;
            _pad = null;
            return false;
        }
        finally
        {
            ReleaseDummies(dummies);
        }
    }

    private void PlaceAt(int target, List<IXbox360Controller> dummies)
    {
        var used = OccupiedSlots();

        for (var slot = 0; slot < target && slot < 4; slot++)
        {
            if (used[slot])
            {
                continue;
            }

            var dummy = _client!.CreateXbox360Controller();
            dummy.AutoSubmitReport = false;
            dummy.Connect();
            dummies.Add(dummy);
        }

        // Give the bus a moment to register the temporary pads.
        Thread.Sleep(150);
    }

    private static void ReleaseDummies(List<IXbox360Controller> dummies)
    {
        foreach (var dummy in dummies)
        {
            try
            {
                dummy.Disconnect();
            }
            catch
            {
                // ignored
            }
        }
    }

    private static bool[] OccupiedSlots()
    {
        var used = new bool[4];
        try
        {
            for (var i = 0; i < 4; i++)
            {
                var state = new NativeMethods.XInputState();
                used[i] = NativeMethods.XInputGetState(i, ref state) == 0;
            }
        }
        catch
        {
            // xinput not available; treat all slots as free
        }

        return used;
    }

    public void Disconnect()
    {
        try
        {
            _pad?.Disconnect();
        }
        catch
        {
            // ignored
        }

        _pad = null;
    }

    public void Submit(PadFrame frame)
    {
        var pad = _pad;
        if (pad is null)
        {
            return;
        }

        try
        {
            pad.ResetReport();

            pad.SetButtonState(Xbox360Button.Up, frame.DpadUp);
            pad.SetButtonState(Xbox360Button.Down, frame.DpadDown);
            pad.SetButtonState(Xbox360Button.Left, frame.DpadLeft);
            pad.SetButtonState(Xbox360Button.Right, frame.DpadRight);
            pad.SetButtonState(Xbox360Button.Start, frame.Menu);
            pad.SetButtonState(Xbox360Button.Back, frame.View);
            pad.SetButtonState(Xbox360Button.LeftThumb, frame.LeftThumb);
            pad.SetButtonState(Xbox360Button.RightThumb, frame.RightThumb);
            pad.SetButtonState(Xbox360Button.LeftShoulder, frame.LeftShoulder);
            pad.SetButtonState(Xbox360Button.RightShoulder, frame.RightShoulder);
            pad.SetButtonState(Xbox360Button.A, frame.A);
            pad.SetButtonState(Xbox360Button.B, frame.B);
            pad.SetButtonState(Xbox360Button.X, frame.X);
            pad.SetButtonState(Xbox360Button.Y, frame.Y);

            pad.SetAxisValue(Xbox360Axis.LeftThumbX, ToAxis(frame.LeftStickX));
            pad.SetAxisValue(Xbox360Axis.LeftThumbY, ToAxis(frame.LeftStickY));
            pad.SetAxisValue(Xbox360Axis.RightThumbX, ToAxis(frame.RightStickX));
            pad.SetAxisValue(Xbox360Axis.RightThumbY, ToAxis(frame.RightStickY));

            pad.SetSliderValue(Xbox360Slider.LeftTrigger, ToTrigger(frame.LeftTrigger));
            pad.SetSliderValue(Xbox360Slider.RightTrigger, ToTrigger(frame.RightTrigger));

            pad.SubmitReport();
        }
        catch (VigemBusNotFoundException)
        {
            _pad = null;
            DriverAvailable = false;
            DriverMessage = "Lost connection to ViGEmBus.";
        }
        catch
        {
            // transient submission failures are ignored
        }
    }

    private static short ToAxis(float value)
    {
        var scaled = Math.Clamp(value, -1f, 1f) * 32767f;
        return (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
    }

    private static byte ToTrigger(float value) =>
        (byte)Math.Clamp(value * 255f, 0f, 255f);

    public void Dispose()
    {
        Disconnect();
        _client?.Dispose();
        _client = null;
    }
}
