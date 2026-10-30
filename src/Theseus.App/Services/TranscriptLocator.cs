using System.Collections.Concurrent;
using System.IO;

namespace Theseus.App.Services;

/// <summary>
/// Finds a session's transcript: <c>~/.claude/projects/&lt;project&gt;/&lt;session id&gt;.jsonl</c>.
///
/// It looks one level down and no deeper. It used to search the whole projects tree, which also
/// holds every subagent and workflow transcript, and it did so for each card on every refresh: on
/// 2026-10-30 that made the board's lifecycle step take 14 to 24 seconds. A session's own transcript
/// is always directly in its project folder, and once found its place is remembered.
/// </summary>
public static class TranscriptLocator
{
    private static readonly ConcurrentDictionary<string, string> Found = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where Claude Code keeps its transcripts on this machine.</summary>
    public static string ProjectsRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>The transcript of this session, or null when there is none.</summary>
    public static string? Find(string? sessionId) => Find(sessionId, ProjectsRoot);

    /// <summary>The transcript of this session under the given projects folder, or null.</summary>
    public static string? Find(string? sessionId, string projectsRoot)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var name = $"{sessionId.Trim()}.jsonl";
        var cacheKey = Path.Combine(projectsRoot, name);

        if (Found.TryGetValue(cacheKey, out var known) && File.Exists(known))
        {
            return known;
        }

        try
        {
            if (!Directory.Exists(projectsRoot))
            {
                return null;
            }

            foreach (var project in Directory.EnumerateDirectories(projectsRoot))
            {
                var candidate = Path.Combine(project, name);

                if (File.Exists(candidate))
                {
                    Found[cacheKey] = candidate;

                    return candidate;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }
}
