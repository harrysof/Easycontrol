using EasyControl.Services;

namespace EasyControl.ViewModels;

public sealed class DeviceItemViewModel
{
    public DeviceItemViewModel(DirectInputDevice device, bool hasProfile)
    {
        Device = device;
        HasProfile = hasProfile;
        Name = device.DisplayName;
        IsXInput = device.IsXInput;
    }

    public DirectInputDevice Device { get; }

    public string Name { get; }

    public bool IsXInput { get; }

    public bool HasProfile { get; }

    public string Subtitle => IsXInput
        ? "XInput device \u2022 already works in games"
        : HasProfile ? "DirectInput \u2022 profile saved" : "DirectInput";

    public string Badge => HasProfile ? "PROFILE" : IsXInput ? "XINPUT" : "DINPUT";
}
