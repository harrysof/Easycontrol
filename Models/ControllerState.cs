namespace EasyControl.Models;

public enum SourceKind
{
    Button,
    Axis,
    Pov
}

/// <summary>A mappable physical input on the controller.</summary>
public sealed record SourceDescriptor(string Key, string Label, SourceKind Kind);

/// <summary>
/// Snapshot of a controller's current input state. Reused between polls to avoid
/// per-frame allocations.
/// </summary>
public sealed class ControllerState
{
    public bool[] Buttons = new bool[32];

    public int X;
    public int Y;
    public int Z;
    public int Rx;
    public int Ry;
    public int Rz;

    public int Slider0;
    public int Slider1;

    /// <summary>-1 when centered, otherwise a DirectInput angle in centi-degrees.</summary>
    public int Pov0 = -1;

    public bool IsConnected;
}
