using System.Diagnostics;
using System.IO;
using System.Text;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>
/// Hands a verified installer to msiexec and restarts Theseus afterwards. Theseus cannot do either
/// itself: its own files are what the MSI replaces, and msiexec has to run elevated while Theseus
/// must not - every session it launches would inherit administrator rights. So a small PowerShell
/// helper, running as Nigel, waits for Theseus to exit, starts msiexec elevated (Windows asks for
/// permission), waits for it, and starts Theseus again whether or not the install went ahead.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>Starts the helper. The caller closes Theseus straight afterwards.</summary>
    public static void Start(string msiPath, string relaunchPath, int theseusProcessId)
    {
        var script = Script(msiPath, relaunchPath, theseusProcessId);
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-EncodedCommand",
                     Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo);
        TheseusLog.Information("Handed {Msi} to the update helper; Theseus restarts from {Path} afterwards", msiPath, relaunchPath);
    }

    /// <summary>The helper script. Paths go in single-quoted literals, so nothing in them is expanded.</summary>
    public static string Script(string msiPath, string relaunchPath, int theseusProcessId)
    {
        var log = Path.Combine(Path.GetDirectoryName(msiPath) ?? Path.GetTempPath(), "install.log");

        return $$"""
            Wait-Process -Id {{theseusProcessId}} -Timeout 120 -ErrorAction SilentlyContinue
            try {
                $arguments = '/i "{0}" /passive /norestart /l*v "{1}"' -f {{Quote(msiPath)}}, {{Quote(log)}}
                Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Verb RunAs -Wait
            } catch {
            }
            Start-Process -FilePath {{Quote(relaunchPath)}}
            """;
    }

    private static string Quote(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
}
