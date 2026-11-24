using System.Diagnostics;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Worktrees;

/// <summary>
/// The real git: a process per call, with no window, no prompt for credentials, and a cap so a stalled call cannot hold
/// the start path for ever (worktree-per-repo AC 11).
/// </summary>
public sealed class GitProcess : IGit
{
    private readonly TimeSpan _cap;

    /// <summary>Creates a runner whose calls are each stopped after <paramref name="cap"/>; 60 s when not given.</summary>
    public GitProcess(TimeSpan? cap = null) => _cap = cap ?? TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public GitResult Run(string workingDirectory, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // A credential prompt would wait for a person who is not there.
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var git = Process.Start(start);

            if (git is null)
            {
                return new GitResult(-1, string.Empty, "git could not be started");
            }

            var output = git.StandardOutput.ReadToEndAsync();
            var error = git.StandardError.ReadToEndAsync();

            if (!git.WaitForExit(_cap))
            {
                git.Kill(entireProcessTree: true);
                TheseusLog.Warning("git {Arguments} in {Folder} was stopped after {Cap}", string.Join(' ', arguments), workingDirectory, _cap);

                return new GitResult(-1, string.Empty, $"git {arguments.FirstOrDefault()} took longer than {_cap.TotalSeconds:0} s and was stopped");
            }

            return new GitResult(git.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException
                                              or IOException)
        {
            TheseusLog.Warning(exception, "git {Arguments} could not run in {Folder}", string.Join(' ', arguments), workingDirectory);

            return new GitResult(-1, string.Empty, $"git could not run: {exception.Message}");
        }
    }
}
