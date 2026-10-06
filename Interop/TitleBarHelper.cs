using System;
using System.Windows;
using System.Windows.Interop;

namespace EasyControl.Interop;

/// <summary>Applies a dark title bar to a WPF window via the DWM API.</summary>
public static class TitleBarHelper
{
    public static void ApplyDark(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var useDark = 1;
            if (NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
            {
                NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaUseImmersiveDarkModeLegacy, ref useDark, sizeof(int));
            }
        }
        catch
        {
            // non-fatal; window simply keeps the default title bar
        }
    }
}
