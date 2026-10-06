using System;
using System.Collections.Generic;
using EasyControl.Models;

namespace EasyControl.Services;

/// <summary>
/// Pure translation from a <see cref="ControllerState"/> through a
/// <see cref="GameProfile"/> into a <see cref="PadFrame"/>.
/// Keeps the previous frame's button state to fire edge-triggered actions.
/// </summary>
public sealed class InputMappingEngine
{
    private readonly Dictionary<string, bool> _previous = new(StringComparer.Ordinal);

    public void Apply(GameProfile profile, ControllerState state, PadFrame frame)
    {
        frame.Reset();

        var deadzone = (float)profile.Deadzone;

        foreach (var binding in profile.Bindings)
        {
            if (string.IsNullOrEmpty(binding.Source) || binding.Target == Targets.None)
            {
                continue;
            }

            ApplyBinding(frame, binding, state, deadzone);
        }

        if (!string.IsNullOrEmpty(profile.ScreenshotSource) && IsControllerSource(profile.ScreenshotSource))
        {
            var pressed = IsDigital(state, profile.ScreenshotSource);
            if (Edge("::screenshot", pressed))
            {
                frame.Screenshot = true;
            }
        }

        if (!string.IsNullOrEmpty(profile.GameBarSource) && IsControllerSource(profile.GameBarSource))
        {
            var pressed = IsDigital(state, profile.GameBarSource);
            if (Edge("::gamebar", pressed))
            {
                frame.GameBar = true;
            }
        }
    }

    private static bool IsControllerSource(string key) =>
        (key.Length > 1 && key[0] == 'B' && char.IsDigit(key[1]))
        || key.StartsWith("Pov", StringComparison.Ordinal);

    private void ApplyBinding(PadFrame frame, InputBinding binding, ControllerState state, float deadzone)
    {
        var isDigitalSource = IsDigitalSource(binding.Source);
        var pressed = false;
        var analog = 0f;
        var isBipolar = false;

        if (isDigitalSource)
        {
            pressed = IsDigital(state, binding.Source);
        }
        else if (TryAxis(state, binding.Source, out var bipolar, out var unipolar))
        {
            isBipolar = IsBipolar(binding.Source);
            analog = isBipolar ? bipolar : unipolar;
            pressed = isBipolar ? MathF.Abs(bipolar) > 0.6f : unipolar > 0.6f;
        }
        else
        {
            return;
        }

        if (binding.Invert && !isDigitalSource)
        {
            analog = -analog;
        }

        switch (binding.Target)
        {
            case Targets.None:
                return;

            case Targets.Screenshot:
                if (Edge(binding.Source, pressed))
                {
                    frame.Screenshot = true;
                }
                return;

            case Targets.LeftThumbX:
                frame.LeftStickX = Deadzone(analog, deadzone);
                return;
            case Targets.LeftThumbY:
                frame.LeftStickY = Deadzone(analog, deadzone);
                return;
            case Targets.RightThumbX:
                frame.RightStickX = Deadzone(analog, deadzone);
                return;
            case Targets.RightThumbY:
                frame.RightStickY = Deadzone(analog, deadzone);
                return;

            case Targets.LeftTriggerAxis:
                frame.LeftTrigger = TriggerValue(pressed, analog, isDigitalSource, isBipolar);
                return;
            case Targets.RightTriggerAxis:
                frame.RightTrigger = TriggerValue(pressed, analog, isDigitalSource, isBipolar);
                return;
        }

        // Otherwise it is a digital XInput button.
        SetButton(frame, binding.Target, pressed);
    }

    private bool Edge(string key, bool pressed)
    {
        _previous.TryGetValue(key, out var was);
        _previous[key] = pressed;
        return pressed && !was;
    }

    private static void SetButton(PadFrame f, string target, bool pressed)
    {
        switch (target)
        {
            case Targets.A: f.A |= pressed; break;
            case Targets.B: f.B |= pressed; break;
            case Targets.X: f.X |= pressed; break;
            case Targets.Y: f.Y |= pressed; break;
            case Targets.LeftShoulder: f.LeftShoulder |= pressed; break;
            case Targets.RightShoulder: f.RightShoulder |= pressed; break;
            case Targets.View: f.View |= pressed; break;
            case Targets.Menu: f.Menu |= pressed; break;
            case Targets.LeftThumb: f.LeftThumb |= pressed; break;
            case Targets.RightThumb: f.RightThumb |= pressed; break;
            case Targets.DpadUp: f.DpadUp |= pressed; break;
            case Targets.DpadDown: f.DpadDown |= pressed; break;
            case Targets.DpadLeft: f.DpadLeft |= pressed; break;
            case Targets.DpadRight: f.DpadRight |= pressed; break;
            case Targets.Guide: f.Guide |= pressed; break;
            case Targets.LeftTrigger: f.LeftTrigger = MathF.Max(f.LeftTrigger, pressed ? 1f : 0f); break;
            case Targets.RightTrigger: f.RightTrigger = MathF.Max(f.RightTrigger, pressed ? 1f : 0f); break;
        }
    }

    private static float TriggerValue(bool pressed, float analog, bool digitalSource, bool bipolar)
    {
        if (digitalSource)
        {
            return pressed ? 1f : 0f;
        }

        return bipolar ? MathF.Max(0f, analog) : analog;
    }

    private static float Deadzone(float value, float deadzone)
    {
        var magnitude = MathF.Abs(value);
        if (magnitude <= deadzone)
        {
            return 0f;
        }

        return MathF.Sign(value) * ((magnitude - deadzone) / (1f - deadzone));
    }

    private static bool IsDigitalSource(string key) =>
        (key.Length > 1 && key[0] == 'B' && char.IsDigit(key[1]))
        || key.StartsWith("Pov", StringComparison.Ordinal);

    private static bool IsBipolar(string key) => key is
        SourceCatalog.AxisX or SourceCatalog.AxisY or SourceCatalog.AxisZ
        or SourceCatalog.AxisRx or SourceCatalog.AxisRy or SourceCatalog.AxisRz;

    private static bool IsDigital(ControllerState s, string key)
    {
        if (key.Length > 1 && key[0] == 'B' && int.TryParse(key.AsSpan(1), out var n))
        {
            return n >= 1 && n <= s.Buttons.Length && s.Buttons[n - 1];
        }

        return key switch
        {
            SourceCatalog.Pov0Up => IsPov(s.Pov0, 31500, 4500),
            SourceCatalog.Pov0Right => IsPov(s.Pov0, 4500, 13500),
            SourceCatalog.Pov0Down => IsPov(s.Pov0, 13500, 22500),
            SourceCatalog.Pov0Left => IsPov(s.Pov0, 22500, 31500),
            _ => false
        };
    }

    private static bool IsPov(int angle, int from, int to)
    {
        if (angle < 0)
        {
            return false;
        }

        return from <= to ? angle >= from && angle <= to : angle >= from || angle <= to;
    }

    private static bool TryAxis(ControllerState s, string key, out float bipolar, out float unipolar)
    {
        bipolar = 0f;
        unipolar = 0f;

        switch (key)
        {
            case SourceCatalog.AxisX: bipolar = Norm(s.X); return true;
            case SourceCatalog.AxisY: bipolar = Norm(s.Y); return true;
            case SourceCatalog.AxisZ: bipolar = Norm(s.Z); return true;
            case SourceCatalog.AxisRx: bipolar = Norm(s.Rx); return true;
            case SourceCatalog.AxisRy: bipolar = Norm(s.Ry); return true;
            case SourceCatalog.AxisRz: bipolar = Norm(s.Rz); return true;
            case SourceCatalog.Slider0: unipolar = s.Slider0 / 65535f; return true;
            case SourceCatalog.Slider1: unipolar = s.Slider1 / 65535f; return true;
            default: return false;
        }
    }

    private static float Norm(int value) =>
        Math.Clamp((value - 32767.5f) / 32767.5f, -1f, 1f);
}
