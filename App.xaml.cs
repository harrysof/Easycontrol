using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using EasyControl.Interop;
using EasyControl.Services;
using EasyControl.ViewModels;
using EasyControl.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EasyControl;

public partial class App : Application
{
    private ServiceProvider? _services;
    private ShellViewModel? _shell;
    private TrayService? _tray;
    private ControllerManager? _manager;
    private SettingsService? _settings;
    private MainWindow? _window;
    private SingleInstance? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) => LogCrash(args.Exception);

        try
        {
            StartCore();
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            Shutdown();
        }
    }

    private static void LogCrash(Exception? exception)
    {
        try
        {
            var folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasyControl");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(folder, "crash.log"),
                $"[{DateTime.Now:u}] {exception}\n\n");
        }
        catch
        {
            // ignored
        }
    }

    private void StartCore()
    {
        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsFirstInstance)
        {
            SingleInstance.SignalExistingInstance();
            Shutdown();
            return;
        }

        _singleInstance.Activated += OnSecondInstanceActivated;

        var services = new ServiceCollection();
        services.AddSingleton<DirectInputService>();
        services.AddSingleton<VigemPadService>();
        services.AddSingleton<HidHideService>();
        services.AddSingleton<PrerequisiteService>();
        services.AddSingleton<KeyboardHookService>();
        services.AddSingleton<ControllerManager>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<StartupService>();
        _services = services.BuildServiceProvider();

        _manager = _services.GetRequiredService<ControllerManager>();
        _settings = _services.GetRequiredService<SettingsService>();
        var profiles = _services.GetRequiredService<ProfileService>();
        var startup = _services.GetRequiredService<StartupService>();
        var directInput = _services.GetRequiredService<DirectInputService>();
        var hidHide = _services.GetRequiredService<HidHideService>();
        var prerequisites = _services.GetRequiredService<PrerequisiteService>();

        // If a previous run left the physical controller hidden (e.g. it was killed
        // while hiding), restore it before anything else.
        hidHide.RecoverIfNeeded();

        _tray = new TrayService();
        _tray.OpenRequested += ShowMainWindow;
        _tray.SettingsRequested += () =>
        {
            ShowMainWindow();
            _shell?.ShowSettings();
        };
        _tray.QuitRequested += Quit;

        _shell = new ShellViewModel(directInput, profiles, _settings, startup, _manager, hidHide, prerequisites, ApplyTrayVisibility);

        _window = new MainWindow { DataContext = _shell };
        MainWindow = _window;

        var handle = new WindowInteropHelper(_window).EnsureHandle();
        directInput.SetMessageWindow(handle);
        TitleBarHelper.ApplyDark(_window);

        _window.Closing += OnWindowClosing;

        _shell.TryAutoActivateDefault();

        ApplyTrayVisibility();

        if (!_settings.Settings.StartMinimized || _settings.Settings.HideTrayIcon)
        {
            _window.Show();
        }

        EnsurePrerequisites(prerequisites, hidHide);
    }

    /// <summary>
    /// On first run, offers to install the bundled ViGEmBus/HidHide drivers so the
    /// app is usable without any manual downloads.
    /// </summary>
    private static void EnsurePrerequisites(PrerequisiteService prerequisites, HidHideService hidHide)
    {
        if (prerequisites.AllInstalled || !prerequisites.InstallersBundled)
        {
            return;
        }

        var choice = MessageBox.Show(
            "EasyControl needs two small drivers to create a virtual Xbox 360 controller and to hide your physical controller.\n\n" +
            "Install the bundled components now? This asks for administrator approval once.",
            "EasyControl setup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (choice != MessageBoxResult.Yes)
        {
            return;
        }

        var (success, message) = prerequisites.InstallMissing();
        hidHide.Refresh();

        MessageBox.Show(
            success ? "Components installed. You're ready to go." : $"Installation did not complete: {message}",
            "EasyControl setup",
            MessageBoxButton.OK,
            success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void ApplyTrayVisibility()
    {
        if (_settings!.Settings.HideTrayIcon)
        {
            _tray!.Hide();
        }
        else
        {
            _tray!.Show();
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_tray is { IsVisible: true })
        {
            e.Cancel = true;
            _window?.Hide();
        }
        else
        {
            Quit();
        }
    }

    private void OnSecondInstanceActivated() => Dispatcher.Invoke(ShowMainWindow);

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
    }

    private void Quit() => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        _window?.Hide();
        _manager?.Dispose();
        _tray?.Dispose();
        _services?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
