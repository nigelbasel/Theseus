using System.Diagnostics;

namespace Theseus.Core.Running;

/// <summary>
/// Whether a headless run made progress: a new commit in any of its card's repositories, or a call to a Theseus tool
/// that writes a record. A handoff alone is not progress, since a card that loops writes handoffs (Nigel, 2026-11-24).
/// </summary>
public static class RunProgress
{
    /// <summary>The Theseus tools whose calls count as progress.</summary>
    public static readonly IReadOnlySet<string> RecordTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "record_round", "tick_criterion", "record_decision", "record_work_item", "report_back", "add_objective", "set_criteria", "set_waiting",
    };

    /// <summary>The HEAD commit of each folder that is in a git repository, by folder.</summary>
    public static IReadOnlyDictionary<string, string> Heads(IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var heads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Head(folder) is { } head)
            {
                heads[folder] = head;
            }
        }

        return heads;
    }

    /// <summary>Whether any folder's HEAD moved, or a record tool was called.</summary>
    public static bool Made(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, int recordCalls)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return recordCalls > 0
               || after.Any(pair => !before.TryGetValue(pair.Key, out var old) || !string.Equals(old, pair.Value, StringComparison.Ordinal));
    }

    private static string? Head(string folder)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(folder);
            startInfo.ArgumentList.Add("rev-parse");
            startInfo.ArgumentList.Add("HEAD");

            using var git = Process.Start(startInfo);

            if (git is null)
            {
                return null;
            }

            var output = git.StandardOutput.ReadToEnd().Trim();

            return git.WaitForExit(10_000) && git.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
