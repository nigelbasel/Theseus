using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Theseus.Core.Projects;

/// <summary>Runs git as a child process, with its output captured. Only the read-only commands of <see cref="GitRepositoryReader"/> are sent.</summary>
public sealed class GitCli : IGitReader
{
    private readonly string _executable;
    private readonly TimeSpan _timeout;

    /// <summary>Creates a runner over the git on the path, or one chosen by hand.</summary>
    public GitCli(string executable = "git", TimeSpan? timeout = null)
    {
        _executable = executable;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    /// <inheritdoc />
    public GitOutput Run(string folder, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(folder);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("core.quotepath=off");

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Reading must never take git's index lock: a session committing in the same folder would fail on it.
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return GitOutput.NotRun($"{_executable} could not be started");
            }

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(_timeout))
            {
                process.Kill(entireProcessTree: true);

                return GitOutput.NotRun($"git did not answer within {_timeout.TotalSeconds:0} s");
            }

            return new GitOutput(true, process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return GitOutput.NotRun($"git could not be run: {exception.Message}");
        }
    }
}
