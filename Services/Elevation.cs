using System;
using System.ComponentModel;
using System.Diagnostics;

namespace EasyControl.Services;

/// <summary>Runs a PowerShell snippet elevated through a single UAC prompt.</summary>
public static class Elevation
{
    public static (bool Success, int ExitCode, string Message) RunPowerShell(string script)
    {
        try
        {
            var info = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            using var process = Process.Start(info);
            if (process is null)
            {
                return (false, -1, "Could not start the elevated process.");
            }

            process.WaitForExit();
            return process.ExitCode == 0
                ? (true, 0, "Applied.")
                : (false, process.ExitCode, $"The elevated process returned exit code {process.ExitCode}.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, 1223, "Elevation was cancelled.");
        }
        catch (Exception ex)
        {
            return (false, -1, ex.Message);
        }
    }

    public static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
