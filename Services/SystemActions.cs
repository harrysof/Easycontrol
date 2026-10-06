using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using EasyControl.Interop;

namespace EasyControl.Services;

/// <summary>
/// Takes a screenshot when the user's assigned button is pressed.
///
/// It first synthesises the Windows shortcut (Win+PrtScn) exactly as the user
/// requested, then verifies that Windows produced a file. If it did not (the
/// shortcut can be remapped or blocked), it falls back to capturing the screen
/// directly. Either way the image lands in Pictures\Screenshots and the clipboard.
/// All work happens off the polling thread so mapping is never interrupted.
/// </summary>
public static class SystemActions
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static void TakeScreenshot()
    {
        if (!Gate.Wait(0))
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                Capture();
            }
            catch
            {
                // a failed screenshot must never crash the mapping loop
            }
            finally
            {
                Gate.Release();
            }
        });
    }

    private static void Capture()
    {
        var folder = ScreenshotFolder();
        Directory.CreateDirectory(folder);

        var before = NewestWrite(folder);

        SendWinPlus(NativeMethods.VkSnapshot);

        if (WaitForNewFile(folder, before, TimeSpan.FromMilliseconds(1500)))
        {
            return;
        }

        DirectCapture(folder);
    }

    // ---- Win+G (Xbox Game Bar) -------------------------------------------

    public static void OpenGameBar()
    {
        if (!Gate.Wait(0))
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                SendWinPlus(VkG);
            }
            catch
            {
                // ignore
            }
            finally
            {
                Gate.Release();
            }
        });
    }

    private const ushort VkG = 0x47;

    // ---- Win+PrtScn -------------------------------------------------------

    private static void SendWinPlus(ushort key)
    {
        var inputs = new[]
        {
            Key(NativeMethods.VkLWin, down: true),
            Key(key, down: true),
            Key(key, down: false),
            Key(NativeMethods.VkLWin, down: false)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }

    private static NativeMethods.Input Key(ushort virtualKey, bool down) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Union = new NativeMethods.InputUnion
        {
            Ki = new NativeMethods.KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = 0,
                Flags = down ? 0u : NativeMethods.KeyEventKeyUp,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        }
    };

    private static bool WaitForNewFile(string folder, DateTime before, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (NewestWrite(folder) > before)
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return NewestWrite(folder) > before;
    }

    private static DateTime NewestWrite(string folder)
    {
        try
        {
            var newest = Directory.EnumerateFiles(folder, "*.png")
                .Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
            return newest;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    // ---- Fallback: direct capture ----------------------------------------

    private static void DirectCapture(string folder)
    {
        var x = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var y = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen));
        var height = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen));

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        }

        var index = 1;
        string path;
        do
        {
            path = Path.Combine(folder, $"Screenshot ({index}).png");
            index++;
        }
        while (File.Exists(path));

        bitmap.Save(path, ImageFormat.Png);
        CopyToClipboard(bitmap);
    }

    private static void CopyToClipboard(Bitmap bitmap)
    {
        try
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return;
            }

            if (dispatcher.CheckAccess())
            {
                Clipboard.SetImage(image);
            }
            else
            {
                dispatcher.Invoke(() => Clipboard.SetImage(image));
            }
        }
        catch
        {
            // clipboard may be locked by another process
        }
    }

    private static string ScreenshotFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "Screenshots");
}
