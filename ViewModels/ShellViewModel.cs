using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyControl.Services;

namespace EasyControl.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly DirectInputService _directInput;
    private readonly ProfileService _profiles;
    private readonly SettingsService _settings;
    private readonly StartupService _startup;
    private readonly ControllerManager _manager;
    private readonly HidHideService _hidHide;
    private readonly PrerequisiteService _prerequisites;
    private readonly Action _applyTrayVisibility;

    private ProfileViewModel? _activeProfile;

    public ShellViewModel(
        DirectInputService directInput,
        ProfileService profiles,
        SettingsService settings,
        StartupService startup,
        ControllerManager manager,
        HidHideService hidHide,
        PrerequisiteService prerequisites,
        Action applyTrayVisibility)
    {
        _directInput = directInput;
        _profiles = profiles;
        _settings = settings;
        _startup = startup;
        _manager = manager;
        _hidHide = hidHide;
        _prerequisites = prerequisites;
        _applyTrayVisibility = applyTrayVisibility;

        ShowDevices();
    }

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _title = "Controllers";

    [ObservableProperty]
    private bool _canGoBack;

    public void ShowDevices()
    {
        _activeProfile?.Deactivate();
        _activeProfile = null;

        CurrentPage = new DevicesViewModel(_directInput, _profiles, _manager, OpenProfile);
        Title = "Controllers";
        CanGoBack = false;
    }

    public void ShowSettings()
    {
        CurrentPage = new SettingsViewModel(_settings, _startup, _hidHide, _prerequisites, ShowDevices, _applyTrayVisibility);
        Title = "Settings";
        CanGoBack = true;
    }

    [RelayCommand]
    private void GoHome() => ShowDevices();

    [RelayCommand]
    private void OpenSettings() => ShowSettings();

    public void OpenProfile(DirectInputDevice device)
    {
        var identity = DevicesViewModel.ToIdentity(device);
        var profile = _profiles.GetOrCreate(identity, device.DisplayName);

        _activeProfile?.Deactivate();
        _activeProfile = new ProfileViewModel(profile, device, _directInput, _manager, _profiles, ShowDevices);

        CurrentPage = _activeProfile;
        Title = profile.Name;
        CanGoBack = true;
    }

    /// <summary>Opens the default profile automatically when its controller is connected.</summary>
    public void TryAutoActivateDefault()
    {
        if (!_settings.Settings.AutoActivateDefault)
        {
            return;
        }

        var defaultProfile = _profiles.FindDefault();
        if (defaultProfile is null)
        {
            return;
        }

        try
        {
            var device = _directInput.Enumerate()
                .FirstOrDefault(d => DevicesViewModel.ToIdentity(d).MatchKey == defaultProfile.Device.MatchKey);

            if (device is not null)
            {
                OpenProfile(device);
            }
        }
        catch
        {
            // ignore enumeration failures at startup
        }
    }
}
