using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace EasyControl.Services;

/// <summary>A driver EasyControl depends on.</summary>
public enum Prerequisite
{
    ViGEmBus,
    HidHide
}

/// <summary>
/// Detects and installs the drivers EasyControl needs. The official installers are
/// bundled in the application's <c>Installers</c> folder, so a fresh install can be
/// completed without any downloads — a single administrator prompt runs them quietly.
/// </summary>
public sealed class PrerequisiteService
{
    private readonly string _installerRoot;

    public PrerequisiteService()
    {
        _installerRoot = Path.Combine(AppContext.BaseDirectory, "Installers");
    }

    public bool ViGEmInstalled => ServiceExists("ViGEmBus");

    public bool HidHideInstalled => ServiceExists("HidHide");

    public bool AllInstalled => ViGEmInstalled && HidHideInstalled;

    public string? ViGEmInstaller => FindInstaller("ViGEmBus");

    public string? HidHideInstaller => FindInstaller("HidHide");

    public bool InstallersBundled => ViGEmInstaller is not null && HidHideInstaller is not null;

    public static bool ServiceExists(string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private string? FindInstaller(string prefix)
    {
        try
        {
            if (!Directory.Exists(_installerRoot))
            {
                return null;
            }

            return Directory.EnumerateFiles(_installerRoot, $"{prefix}*.exe").FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Silently installs the bundled installers for any missing component using a
    /// single elevated process (one UAC prompt).
    /// </summary>
    public (bool Success, string Message) InstallMissing()
    {
        var jobs = new List<(Prerequisite Prereq, string Path)>();

        if (!ViGEmInstalled && ViGEmInstaller is { } viGEm)
        {
            jobs.Add((Prerequisite.ViGEmBus, viGEm));
        }

        if (!HidHideInstalled && HidHideInstaller is { } hidHide)
        {
            jobs.Add((Prerequisite.HidHide, hidHide));
        }

        if (jobs.Count == 0)
        {
            return AllInstalled
                ? (true, "All components are already installed.")
                : (false, "No bundled installers were found next to the application.");
        }

        var (success, _, message) = Elevation.RunPowerShell(BuildScript(jobs));
        return success
            ? (true, "Components installed.")
            : (false, message);
    }

    private static string BuildScript(IReadOnlyList<(Prerequisite Prereq, string Path)> jobs)
    {
        var builder = new StringBuilder();
        builder.Append("$ErrorActionPreference='Stop'; $code=0; ");

        foreach (var (_, path) in jobs)
        {
            builder.Append("$p = Start-Process -FilePath ")
                   .Append(Elevation.Quote(path))
                   .Append(" -ArgumentList '/quiet','/norestart' -Wait -PassThru; ")
                   .Append("if ($p.ExitCode -ne 0) { $code = $p.ExitCode }; ");
        }

        builder.Append("exit $code");
        return builder.ToString();
    }
}
