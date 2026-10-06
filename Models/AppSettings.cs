namespace EasyControl.Models;

/// <summary>User-configurable application settings.</summary>
public sealed class AppSettings
{
    /// <summary>Launch EasyControl when Windows starts.</summary>
    public bool RunAtStartup { get; set; }

    /// <summary>Hide the notification-area (tray) icon.</summary>
    public bool HideTrayIcon { get; set; }

    /// <summary>Start with the window hidden to the tray/taskbar.</summary>
    public bool StartMinimized { get; set; }

    /// <summary>Automatically activate the default profile for a connected controller.</summary>
    public bool AutoActivateDefault { get; set; } = true;

    /// <summary>Hide the physical controller (via HidHide) while mapping is active.</summary>
    public bool HidePhysical { get; set; }

    /// <summary>Preferred XInput player slot: 0 = automatic, 1-4 = first..fourth.</summary>
    public int VirtualControllerPosition { get; set; }
}
