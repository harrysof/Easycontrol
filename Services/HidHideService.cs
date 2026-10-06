using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using EasyControl.Interop;
using Microsoft.Win32;

namespace EasyControl.Services;

/// <summary>
/// Drives the HidHide driver so the physical controller is hidden from other
/// applications while EasyControl is feeding the virtual Xbox 360 pad.
/// Requires HidHide to be installed; changes run through one elevated call.
///
/// Hiding is journalled to disk so that, even if EasyControl is killed while a
/// device is hidden, the next launch (or a manual restore) un-hides it again.
/// </summary>
public sealed class HidHideService
{
    private const string ServiceKeyPath = @"SYSTEM\CurrentControlSet\Services\HidHide";

    private static readonly string[] CandidateCliPaths =
    [
        @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
        @"C:\Program Files\HidHide\HidHideCLI.exe",
        @"C:\Program Files (x86)\HidHide\HidHideCLI.exe",
        @"C:\Program Files (x86)\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe"
    ];

    private static readonly string PendingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EasyControl",
        "hidhide.json");

    private readonly SemaphoreSlim _gate = new(1, 1);

    public HidHideService()
    {
        CliPath = LocateCli();
    }

    public string? CliPath { get; private set; }

    public bool IsInstalled => CliPath is not null;

    /// <summary>True when the HidHide kernel service is registered.</summary>
    public static bool ServiceInstalled
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(ServiceKeyPath);
                return key is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Re-scans for the CLI (e.g. after an on-demand install).</summary>
    public bool Refresh()
    {
        CliPath = LocateCli();
        return IsInstalled;
    }

    public static string DownloadUrl => "https://github.com/nefarius/HidHide/releases/latest";

    public void OpenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true });
        }
        catch
        {
            // ignored
        }
    }

    public IReadOnlyList<string> FindInstanceIds(DirectInputDevice device) =>
        SetupApi.FindHidInstanceIds(device.HardwareId);

    /// <summary>Hides or unhides the given devices and toggles the cloak.</summary>
    public (bool Success, string Message) Apply(bool hide, IReadOnlyList<string> instanceIds)
    {
        if (!IsInstalled)
        {
            return (false, "HidHide is not installed.");
        }

        if (instanceIds.Count == 0)
        {
            return (false, "No matching HID device was found to hide.");
        }

        _gate.Wait();
        try
        {
            var exe = Environment.ProcessPath ?? string.Empty;
            var script = BuildApplyScript(hide, exe, instanceIds);
            var (success, _, message) = Elevation.RunPowerShell(script);

            if (success)
            {
                if (hide)
                {
                    SavePending(instanceIds);
                }
                else
                {
                    ClearPending();
                }
            }

            return (success, success ? "Applied." : message);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Un-hides anything EasyControl previously hid and turns the cloak off.
    /// Safe to call repeatedly; runs elevated only when there is something to undo.
    /// </summary>
    public (bool Success, string Message) RestoreAll()
    {
        if (!IsInstalled)
        {
            return (false, "HidHide is not installed.");
        }

        var pending = LoadPending();
        if (pending.Count == 0 && !IsCloaked())
        {
            ClearPending();
            return (true, "Nothing to restore.");
        }

        _gate.Wait();
        try
        {
            var cli = Elevation.Quote(CliPath!);
            var builder = new StringBuilder();
            builder.Append("$ErrorActionPreference='SilentlyContinue'; ");

            foreach (var id in pending)
            {
                builder.Append("& ").Append(cli).Append(" --dev-unhide ").Append(Elevation.Quote(id)).Append("; ");
            }

            builder.Append("& ").Append(cli).Append(" --cloak-off");

            var (success, _, message) = Elevation.RunPowerShell(builder.ToString());
            if (success)
            {
                ClearPending();
            }

            return (success, success ? "Restored." : message);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs at startup: if a previous run left devices hidden (e.g. after being
    /// killed), un-hide them so the controller is never stuck invisible.
    /// </summary>
    public void RecoverIfNeeded()
    {
        if (!IsInstalled)
        {
            return;
        }

        var pending = LoadPending();
        if (pending.Count == 0 && !IsCloaked())
        {
            ClearPending();
            return;
        }

        RestoreAll();
    }

    private bool IsCloaked()
    {
        var state = RunCli("--cloak-state");
        return state.Contains("--cloak-on", StringComparison.OrdinalIgnoreCase);
    }

    private string RunCli(params string[] args)
    {
        try
        {
            if (CliPath is null)
            {
                return string.Empty;
            }

            var info = new ProcessStartInfo(CliPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            using var process = Process.Start(info);
            if (process is null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildApplyScript(bool hide, string exePath, IReadOnlyList<string> instanceIds)
    {
        var cli = Elevation.Quote(CliPath!);
        var builder = new StringBuilder();

        if (hide)
        {
            builder.Append("& ").Append(cli).Append(" --app-reg ").Append(Elevation.Quote(exePath)).Append("; ");

            foreach (var id in instanceIds)
            {
                builder.Append("& ").Append(cli).Append(" --dev-hide ").Append(Elevation.Quote(id)).Append("; ");
            }

            builder.Append("& ").Append(cli).Append(" --cloak-on");
        }
        else
        {
            foreach (var id in instanceIds)
            {
                builder.Append("& ").Append(cli).Append(" --dev-unhide ").Append(Elevation.Quote(id)).Append("; ");
            }

            builder.Append("& ").Append(cli).Append(" --cloak-off");
        }

        return builder.ToString();
    }

    // ---- Pending-state journal -------------------------------------------

    private static IReadOnlyList<string> LoadPending()
    {
        try
        {
            if (!File.Exists(PendingPath))
            {
                return [];
            }

            var json = File.ReadAllText(PendingPath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SavePending(IReadOnlyList<string> instanceIds)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)!);
            File.WriteAllText(PendingPath, JsonSerializer.Serialize(instanceIds));
        }
        catch
        {
            // best effort
        }
    }

    private static void ClearPending()
    {
        try
        {
            if (File.Exists(PendingPath))
            {
                File.Delete(PendingPath);
            }
        }
        catch
        {
            // best effort
        }
    }

    // ---- CLI location -----------------------------------------------------

    private static string? LocateCli()
    {
        foreach (var candidate in CandidateCliPaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // The MSI records its install directory; the CLI lives in the x64 folder.
        foreach (var root in UninstallInstallLocations("HidHide"))
        {
            var found = SearchForCli(root);
            if (found is not null)
            {
                return found;
            }
        }

        foreach (var root in CommonRoots())
        {
            var found = SearchForCli(root);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<string> UninstallInstallLocations(string match)
    {
        var keys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var path in keys)
        {
            string[] subKeys;
            try
            {
                using var root = Registry.LocalMachine.OpenSubKey(path);
                subKeys = root?.GetSubKeyNames() ?? [];
            }
            catch
            {
                continue;
            }

            foreach (var subKey in subKeys)
            {
                string? location = null;
                string? displayName = null;
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey($@"{path}\{subKey}");
                    displayName = key?.GetValue("DisplayName") as string;
                    location = key?.GetValue("InstallLocation") as string;
                }
                catch
                {
                    continue;
                }

                if (displayName is not null
                    && displayName.Contains(match, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(location))
                {
                    yield return location!;
                }
            }
        }
    }

    private static IEnumerable<string> CommonRoots()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        return roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r));
    }

    private static string? SearchForCli(string root)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return null;
            }

            foreach (var file in Directory.EnumerateFiles(root, "HidHideCLI.exe", SearchOption.AllDirectories))
            {
                return file;
            }
        }
        catch
        {
            // permission or IO issues — ignore
        }

        return null;
    }
}
