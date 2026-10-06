using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyControl.Models;
using EasyControl.Services;

namespace EasyControl.ViewModels;

public sealed partial class DevicesViewModel : ObservableObject
{
    private readonly DirectInputService _directInput;
    private readonly ProfileService _profiles;
    private readonly ControllerManager _manager;
    private readonly Action<DirectInputDevice> _onSelected;

    public DevicesViewModel(
        DirectInputService directInput,
        ProfileService profiles,
        ControllerManager manager,
        Action<DirectInputDevice> onSelected)
    {
        _directInput = directInput;
        _profiles = profiles;
        _manager = manager;
        _onSelected = onSelected;
        Refresh();
    }

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = [];

    public AppSettings Settings { get; set; } = new();

    [ObservableProperty]
    private string _driverStatus = string.Empty;

    [ObservableProperty]
    private bool _driverMissing;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isDefaultActive;

    [ObservableProperty]
    private string _defaultStatus = string.Empty;

    [RelayCommand]
    private void Refresh()
    {
        Devices.Clear();

        _manager.EnableDriverProbe();

        DriverMissing = !_manager.DriverAvailable;
        DriverStatus = DriverMissing
            ? _manager.DriverMessage ?? "ViGEmBus not detected."
            : "ViGEmBus ready \u2022 virtual Xbox 360 output available";

        try
        {
            foreach (var device in _directInput.Enumerate())
            {
                var identity = ToIdentity(device);
                var hasProfile = _profiles.FindForDevice(identity) is not null;
                Devices.Add(new DeviceItemViewModel(device, hasProfile));
            }
        }
        catch
        {
            // enumeration failed; leave list empty
        }

        IsEmpty = Devices.Count == 0;

        var defaultProfile = _profiles.FindDefault();
        IsDefaultActive = defaultProfile is not null && ManagerIsActive;
        DefaultStatus = defaultProfile is null
            ? "No default profile set"
            : $"Default profile: {defaultProfile.Name}";
    }

    [RelayCommand]
    private void Select(DeviceItemViewModel? item)
    {
        if (item is not null)
        {
            _onSelected(item.Device);
        }
    }

    private bool ManagerIsActive => _manager.IsOutputConnected;

    public static DeviceIdentity ToIdentity(DirectInputDevice device) => new()
    {
        ProductName = device.Instance.ProductName ?? string.Empty,
        ProductGuid = device.Instance.ProductGuid,
        Usage = (int)device.Instance.Usage,
        UsagePage = (int)device.Instance.UsagePage
    };
}
