using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using EasyControl.Interop;

namespace EasyControl.Services;

/// <summary>
/// Global low-level keyboard hook that lets EasyControl handle buttons which do
/// not appear on the game controller's DirectInput interface — most importantly
/// the controller's Guide/Home key, which Windows delivers as a consumer-control
/// keyboard key (and which otherwise opens a browser tab or the Game Bar).
///
/// It has two jobs:
///  * capture the next key pressed so it can be bound to an action, and
///  * swallow that key at runtime so it triggers the mapped action instead.
/// </summary>
public sealed class KeyboardHookService : IDisposable
{
    private const string KeyPrefix = "key:";

    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly Dictionary<int, string> _runtimeBlocks = new();

    private IntPtr _hook;
    private Action<string>? _captureCallback;

    public KeyboardHookService()
    {
        _proc = HookCallback;
    }

    /// <summary>Raised on the hook thread when a runtime-mapped key is pressed.</summary>
    public event Action<string>? ActionTriggered;

    public bool IsCapturing => _captureCallback is not null;

    public bool Install()
    {
        if (_hook != IntPtr.Zero)
        {
            return true;
        }

        var module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _proc, module, 0);
        return _hook != IntPtr.Zero;
    }

    public void BeginCapture(Action<string> onCaptured) => _captureCallback = onCaptured;

    public void CancelCapture() => _captureCallback = null;

    /// <summary>Maps keyboard sources to an action id, blocking them while active.</summary>
    public void SetRuntimeBlocks(IEnumerable<KeyValuePair<string, string>> keyToAction)
    {
        _runtimeBlocks.Clear();
        foreach (var pair in keyToAction)
        {
            if (TryParseKey(pair.Key, out var vk))
            {
                _runtimeBlocks[vk] = pair.Value;
            }
        }
    }

    public void ClearRuntimeBlocks() => _runtimeBlocks.Clear();

    public static bool IsKeyboardSource(string? source) =>
        !string.IsNullOrEmpty(source) && source.StartsWith(KeyPrefix, StringComparison.Ordinal);

    public static string Format(int virtualKey) => $"{KeyPrefix}0x{virtualKey:X}";

    public static bool TryParseKey(string? source, out int virtualKey)
    {
        virtualKey = 0;
        return IsKeyboardSource(source)
            && int.TryParse(source!.AsSpan(KeyPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out virtualKey);
    }

    /// <summary>A human-readable label for any source (controller or keyboard).</summary>
    public static string Label(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return "Not set";
        }

        return TryParseKey(source, out var vk) ? $"Keyboard key (0x{vk:X2})" : source;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            var injected = (info.Flags & NativeMethods.LlkhfInjected) != 0;
            var message = (int)wParam;
            var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;

            if (!injected && isDown)
            {
                var callback = _captureCallback;
                if (callback is not null)
                {
                    _captureCallback = null;
                    callback(Format((int)info.VkCode));
                    return 1;
                }

                if (_runtimeBlocks.TryGetValue((int)info.VkCode, out var action))
                {
                    ActionTriggered?.Invoke(action);
                    return 1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
