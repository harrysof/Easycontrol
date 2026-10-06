using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;

namespace EasyControl.Services;

/// <summary>Notification-area icon and its context menu.</summary>
public sealed class TrayService : IDisposable
{
    private TaskbarIcon? _icon;

    public event Action? OpenRequested;

    public event Action? SettingsRequested;

    public event Action? QuitRequested;

    public bool IsVisible => _icon is not null;

    public void Show()
    {
        if (_icon is not null)
        {
            return;
        }

        var icon = new TaskbarIcon
        {
            ToolTipText = "EasyControl"
        };

        var iconResource = Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/generated/EasyControl.ico"));
        if (iconResource?.Stream is { } iconStream)
        {
            using (iconStream)
            {
                icon.Icon = new System.Drawing.Icon(iconStream);
            }
        }

        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x1E, 0x33)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x33, 0x58)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xED, 0xEA, 0xF5))
        };

        menu.Items.Add(CreateItem("Open EasyControl", () => OpenRequested?.Invoke()));
        menu.Items.Add(CreateItem("Settings", () => SettingsRequested?.Invoke()));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateItem("Quit", () => QuitRequested?.Invoke()));

        icon.ContextMenu = menu;
        icon.TrayLeftMouseUp += (_, _) => OpenRequested?.Invoke();
        icon.TrayMouseDoubleClick += (_, _) => OpenRequested?.Invoke();
        icon.ForceCreate();

        _icon = icon;
    }

    public void Hide()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private static MenuItem CreateItem(string header, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            Foreground = new SolidColorBrush(Color.FromRgb(0xED, 0xEA, 0xF5)),
            Background = Brushes.Transparent
        };

        item.Click += (_, _) => action();
        return item;
    }

    public void Dispose() => Hide();
}
