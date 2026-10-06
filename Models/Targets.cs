using System.Collections.Generic;
using System.Linq;

namespace EasyControl.Models;

/// <summary>All selectable mapping targets.</summary>
public static class Targets
{
    public const string None = "None";
    public const string Screenshot = "Screenshot";

    // XInput buttons (digital)
    public const string A = "A";
    public const string B = "B";
    public const string X = "X";
    public const string Y = "Y";
    public const string LeftShoulder = "LB";
    public const string RightShoulder = "RB";
    public const string LeftTrigger = "LT";
    public const string RightTrigger = "RT";
    public const string View = "View";
    public const string Menu = "Menu";
    public const string LeftThumb = "LS";
    public const string RightThumb = "RS";
    public const string DpadUp = "Up";
    public const string DpadDown = "Down";
    public const string DpadLeft = "Left";
    public const string DpadRight = "Right";
    public const string Guide = "Guide";

    // XInput analog
    public const string LeftThumbX = "LeftStick X";
    public const string LeftThumbY = "LeftStick Y";
    public const string RightThumbX = "RightStick X";
    public const string RightThumbY = "RightStick Y";
    public const string LeftTriggerAxis = "LeftTrigger";
    public const string RightTriggerAxis = "RightTrigger";

    public static readonly IReadOnlyList<string> ButtonTargets =
    [
        None, A, B, X, Y,
        LeftShoulder, RightShoulder, LeftTrigger, RightTrigger,
        View, Menu, LeftThumb, RightThumb,
        DpadUp, DpadDown, DpadLeft, DpadRight,
        Guide, Screenshot
    ];

    public static readonly IReadOnlyList<string> AxisTargets =
    [
        None, LeftThumbX, LeftThumbY, RightThumbX, RightThumbY,
        LeftTriggerAxis, RightTriggerAxis
    ];
}

/// <summary>How a target is captured when the user assigns it.</summary>
public enum TargetKind
{
    Button,
    Trigger,
    Stick
}

/// <summary>An Xbox 360 output the user can bind a physical input to.</summary>
public sealed record TargetDescriptor(string Key, string Label, TargetKind Kind);

/// <summary>The controllers' outputs, listed in Xbox 360 pad order.</summary>
public static class TargetCatalog
{
    public static readonly IReadOnlyList<TargetDescriptor> All =
    [
        new(Targets.A, "A", TargetKind.Button),
        new(Targets.B, "B", TargetKind.Button),
        new(Targets.X, "X", TargetKind.Button),
        new(Targets.Y, "Y", TargetKind.Button),
        new(Targets.LeftShoulder, "LB \u2014 Left Bumper", TargetKind.Button),
        new(Targets.RightShoulder, "RB \u2014 Right Bumper", TargetKind.Button),
        new(Targets.View, "View / Back", TargetKind.Button),
        new(Targets.Menu, "Menu / Start", TargetKind.Button),
        new(Targets.LeftThumb, "L3 \u2014 Left Stick Click", TargetKind.Button),
        new(Targets.RightThumb, "R3 \u2014 Right Stick Click", TargetKind.Button),
        new(Targets.DpadUp, "D-Pad Up", TargetKind.Button),
        new(Targets.DpadDown, "D-Pad Down", TargetKind.Button),
        new(Targets.DpadLeft, "D-Pad Left", TargetKind.Button),
        new(Targets.DpadRight, "D-Pad Right", TargetKind.Button),

        new(Targets.LeftTriggerAxis, "LT \u2014 Left Trigger", TargetKind.Trigger),
        new(Targets.RightTriggerAxis, "RT \u2014 Right Trigger", TargetKind.Trigger),

        new(Targets.LeftThumbX, "Left Stick \u2014 Left / Right", TargetKind.Stick),
        new(Targets.LeftThumbY, "Left Stick \u2014 Up / Down", TargetKind.Stick),
        new(Targets.RightThumbX, "Right Stick \u2014 Left / Right", TargetKind.Stick),
        new(Targets.RightThumbY, "Right Stick \u2014 Up / Down", TargetKind.Stick),
    ];

    public static IReadOnlyList<TargetDescriptor> Buttons { get; } =
        [.. All.Where(d => d.Kind == TargetKind.Button)];

    public static IReadOnlyList<TargetDescriptor> Triggers { get; } =
        [.. All.Where(d => d.Kind == TargetKind.Trigger)];

    public static IReadOnlyList<TargetDescriptor> Sticks { get; } =
        [.. All.Where(d => d.Kind == TargetKind.Stick)];
}

/// <summary>Builds the list of mappable sources for a device.</summary>
public static class SourceCatalog
{
    public const int MaxButtons = 32;

    public const string AxisX = "AxisX";
    public const string AxisY = "AxisY";
    public const string AxisZ = "AxisZ";
    public const string AxisRx = "AxisRX";
    public const string AxisRy = "AxisRY";
    public const string AxisRz = "AxisRZ";
    public const string Slider0 = "Slider0";
    public const string Slider1 = "Slider1";

    public const string Pov0Up = "Pov0Up";
    public const string Pov0Down = "Pov0Down";
    public const string Pov0Left = "Pov0Left";
    public const string Pov0Right = "Pov0Right";

    public static IReadOnlyList<SourceDescriptor> Build(int buttonCount, bool hasZ, bool hasRotation)
    {
        var list = new List<SourceDescriptor>();
        var buttons = Math.Clamp(buttonCount, 0, MaxButtons);

        for (var i = 0; i < buttons; i++)
        {
            list.Add(new SourceDescriptor($"B{i + 1}", $"Button {i + 1}", SourceKind.Button));
        }

        list.Add(new SourceDescriptor(AxisX, "Left Stick X", SourceKind.Axis));
        list.Add(new SourceDescriptor(AxisY, "Left Stick Y", SourceKind.Axis));

        if (hasZ)
        {
            list.Add(new SourceDescriptor(AxisZ, "Z Axis", SourceKind.Axis));
            list.Add(new SourceDescriptor(Slider0, "Slider 1", SourceKind.Axis));
            list.Add(new SourceDescriptor(Slider1, "Slider 2", SourceKind.Axis));
        }

        if (hasRotation)
        {
            list.Add(new SourceDescriptor(AxisRx, "Right Stick X", SourceKind.Axis));
            list.Add(new SourceDescriptor(AxisRy, "Right Stick Y", SourceKind.Axis));
            list.Add(new SourceDescriptor(AxisRz, "RZ Axis", SourceKind.Axis));
        }

        list.Add(new SourceDescriptor(Pov0Up, "D-Pad Up", SourceKind.Pov));
        list.Add(new SourceDescriptor(Pov0Down, "D-Pad Down", SourceKind.Pov));
        list.Add(new SourceDescriptor(Pov0Left, "D-Pad Left", SourceKind.Pov));
        list.Add(new SourceDescriptor(Pov0Right, "D-Pad Right", SourceKind.Pov));

        return list;
    }

    public static string LabelFor(string key)
    {
        if (key.StartsWith('B') && int.TryParse(key[1..], out var n))
        {
            return $"Button {n}";
        }

        return key switch
        {
            AxisX => "Left Stick X",
            AxisY => "Left Stick Y",
            AxisZ => "Z Axis",
            AxisRx => "Right Stick X",
            AxisRy => "Right Stick Y",
            AxisRz => "RZ Axis",
            Slider0 => "Slider 1",
            Slider1 => "Slider 2",
            Pov0Up => "D-Pad Up",
            Pov0Down => "D-Pad Down",
            Pov0Left => "D-Pad Left",
            Pov0Right => "D-Pad Right",
            _ => key
        };
    }
}
