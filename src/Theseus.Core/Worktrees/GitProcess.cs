using System.Diagnostics;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Worktrees;

/// <summary>
/// The real git: a process per call, with no window, no prompt for credentials, and a cap so a stalled call cannot hold
/// the start path for ever (worktree-per-repo AC 11).
/// </summary>
public sealed class GitProcess : IGit
{
    private readonly TimeSpan _queryCap;
    private readonly TimeSpan _changeCap;

    /// <summary>
    /// Creates a runner. A query is stopped after <paramref name="queryCap"/>, 60 s when not given. Making or removing a
    /// worktree gets <paramref name="changeCap"/>, 10 minutes when not given: a large checkout stopped half way leaves a
    /// worktree git has registered but not filled (review of worktree-per-repo, finding 4).
    /// </summary>
    public GitProcess(TimeSpan? queryCap = null, TimeSpan? changeCap = null)
    {
        _queryCap = queryCap ?? TimeSpan.FromSeconds(60);
        _changeCap = changeCap ?? TimeSpan.FromMinutes(10);
    }

    /// <inheritdoc />
    public GitResult Run(string workingDirectory, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var cap = IsChange(arguments) ? _changeCap : _queryCap;

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

            if (!git.WaitForExit(cap))
            {
                git.Kill(entireProcessTree: true);
                TheseusLog.Warning("git {Arguments} in {Folder} was stopped after {Cap}", string.Join(' ', arguments), workingDirectory, cap);

                return new GitResult(-1, string.Empty, $"git {string.Join(' ', arguments.Take(2))} took longer than {cap.TotalSeconds:0} s and was stopped")
                {
                    TimedOut = true,
                };
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

    private static bool IsChange(string[] arguments)
    {
        var at = Array.IndexOf(arguments, "worktree");

        return at >= 0 && at + 1 < arguments.Length && arguments[at + 1] is "add" or "remove";
    }
}
