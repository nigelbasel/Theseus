using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <summary>
/// Counts the sub-agents a session is running outside a workflow. Each sub-agent writes its own
/// transcript in the session's subagents folder as it works. A transcript written to recently is an
/// agent at work; one that has gone quiet is an agent that has returned. Workflow agents sit in a
/// folder below that one, and the workflow journal already counts them.
/// </summary>
public sealed class SubagentActivityReader
{
    /// <summary>How recently a transcript must have been written for its agent to count as running.</summary>
    public static readonly TimeSpan Recent = TimeSpan.FromMinutes(2);

    private readonly string _projects;
    private readonly Dictionary<string, string> _located = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a reader over the projects directory.</summary>
    public SubagentActivityReader(string projects) => _projects = projects;

    /// <summary>How many of the session's sub-agents have written in the last two minutes.</summary>
    public int Running(string? sessionId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || Locate(sessionId) is not { } folder)
        {
            return 0;
        }

        try
        {
            return new DirectoryInfo(folder)
                .EnumerateFiles("*.jsonl", SearchOption.TopDirectoryOnly)
                .Count(transcript => now - transcript.LastWriteTimeUtc < Recent);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not read the sub-agents at {Path}: {Reason}", folder, exception.Message);

            return 0;
        }
    }

    private string? Locate(string sessionId)
    {
        if (_located.TryGetValue(sessionId, out var known) && Directory.Exists(known))
        {
            return known;
        }

        try
        {
            var found = Directory.Exists(_projects)
                ? Directory.EnumerateDirectories(_projects)
                    .Select(project => Path.Combine(project, sessionId, "subagents"))
                    .FirstOrDefault(Directory.Exists)
                : null;

            if (found is not null)
            {
                _located[sessionId] = found;
            }

            return found;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
