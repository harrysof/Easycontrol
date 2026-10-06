using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyControl.Services;

namespace EasyControl.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly StartupService _startup;
    private readonly HidHideService _hidHide;
    private readonly PrerequisiteService _prerequisites;
    private readonly Action _onBack;
    private readonly Action _onTrayChanged;

    public SettingsViewModel(
        SettingsService settings,
        StartupService startup,
        HidHideService hidHide,
        PrerequisiteService prerequisites,
        Action onBack,
        Action onTrayChanged)
    {
        _settings = settings;
        _startup = startup;
        _hidHide = hidHide;
        _prerequisites = prerequisites;
        _onBack = onBack;
        _onTrayChanged = onTrayChanged;

        _runAtStartup = startup.IsEnabled;
        _hideTrayIcon = settings.Settings.HideTrayIcon;
        _startMinimized = settings.Settings.StartMinimized;
        _hidePhysical = settings.Settings.HidePhysical;
        _controllerPosition = settings.Settings.VirtualControllerPosition;
    }

    public IReadOnlyList<string> ControllerPositions { get; } =
        ["Automatic", "1st", "2nd", "3rd", "4th"];

    [ObservableProperty]
    private int _controllerPosition;

    partial void OnControllerPositionChanged(int value)
    {
        _settings.Settings.VirtualControllerPosition = value;
        _settings.Save();
    }

    // ---- Components -------------------------------------------------------

    public bool ViGEmInstalled => _prerequisites.ViGEmInstalled;

    public bool HidHideInstalled => _prerequisites.HidHideInstalled || _hidHide.IsInstalled;

    public bool ComponentsInstalled => ViGEmInstalled && HidHideInstalled;

    public bool ComponentsMissing => !ComponentsInstalled;

    public bool CanInstallComponents => ComponentsMissing && _prerequisites.InstallersBundled;

    public bool ShowGetHidHide => !HidHideInstalled && !_prerequisites.InstallersBundled;

    public string ViGEmStatusText => ViGEmInstalled ? "Installed" : "Not installed";

    public string HidHideStatusText => HidHideInstalled ? "Installed" : "Not installed";

    public string HidHideStatus => HidHideInstalled
        ? "HidHide detected \u2022 the physical controller can be hidden"
        : CanInstallComponents
            ? "Not installed \u2022 install the bundled component to hide the physical controller"
            : "HidHide is not installed \u2022 required to hide the physical controller";

    public bool HidHideMissing => !HidHideInstalled;

    public string DataFolder => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EasyControl");

    [ObservableProperty]
    private bool _isInstallingComponents;

    [ObservableProperty]
    private string _installMessage = string.Empty;

    [RelayCommand]
    private async Task InstallComponentsAsync()
    {
        if (IsInstallingComponents)
        {
            return;
        }

        IsInstallingComponents = true;
        InstallMessage = "Installing\u2026 approve the administrator prompt.";
        NotifyComponents();

        try
        {
            var result = await Task.Run(_prerequisites.InstallMissing);
            _hidHide.Refresh();
            InstallMessage = result.Success
                ? "Components installed."
                : result.Message;
        }
        catch (Exception ex)
        {
            InstallMessage = ex.Message;
        }
        finally
        {
            IsInstallingComponents = false;
            NotifyComponents();
        }
    }

    private void NotifyComponents()
    {
        OnPropertyChanged(nameof(ViGEmInstalled));
        OnPropertyChanged(nameof(HidHideInstalled));
        OnPropertyChanged(nameof(ComponentsInstalled));
        OnPropertyChanged(nameof(ComponentsMissing));
        OnPropertyChanged(nameof(CanInstallComponents));
        OnPropertyChanged(nameof(ShowGetHidHide));
        OnPropertyChanged(nameof(ViGEmStatusText));
        OnPropertyChanged(nameof(HidHideStatusText));
        OnPropertyChanged(nameof(HidHideStatus));
        OnPropertyChanged(nameof(HidHideMissing));
    }

    // ---- Settings ---------------------------------------------------------

    [ObservableProperty]
    private bool _runAtStartup;

    [ObservableProperty]
    private bool _hideTrayIcon;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _hidePhysical;

    partial void OnHidePhysicalChanged(bool value)
    {
        _settings.Settings.HidePhysical = value;
        _settings.Save();

        // Turning hiding off must actually restore the physical controller.
        if (!value)
        {
            _ = Task.Run(() =>
            {
                _hidHide.RestoreAll();
                _hidHide.Refresh();
            });
        }
    }

    [RelayCommand]
    private void GetHidHide() => _hidHide.OpenDownloadPage();

    partial void OnRunAtStartupChanged(bool value)
    {
        _settings.Settings.RunAtStartup = value;
        _startup.Apply(value);
        _settings.Save();
    }

    partial void OnHideTrayIconChanged(bool value)
    {
        _settings.Settings.HideTrayIcon = value;
        _settings.Save();
        _onTrayChanged();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settings.Settings.StartMinimized = value;
        _settings.Save();
    }

    [RelayCommand]
    private void Back() => _onBack();
}
