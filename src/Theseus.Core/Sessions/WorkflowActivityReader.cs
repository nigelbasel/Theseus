using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <summary>
/// Reads the journal of the workflow a session is running.
///
/// A run's summary is written only once it ends, so it says nothing about a run under way. The
/// journal beside it is appended as agents are dispatched and as they return, which is what makes a
/// live run legible at all. Counting its lines is enough and avoids parsing a file being written to.
/// </summary>
public sealed class WorkflowActivityReader : IWorkflowActivityReader
{
    private const string StartedMarker = "\"type\":\"started\"";
    private const string ResultMarker = "\"type\":\"result\"";

    private readonly string _projects;

    /// <summary>Creates a reader over the projects directory.</summary>
    public WorkflowActivityReader(string projects) => _projects = projects;

    /// <inheritdoc />
    public WorkflowActivity? Read(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || Locate(sessionId) is not { } runs)
        {
            return null;
        }

        try
        {
            return Directory.EnumerateDirectories(runs)
                .Select(Journal)
                .OfType<FileInfo>()
                .OrderByDescending(journal => journal.LastWriteTimeUtc)
                .Select(Count)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not read the workflow journals at {Path}: {Reason}", runs, exception.Message);

            return null;
        }
    }

    private string? Locate(string sessionId)
    {
        try
        {
            return Directory.Exists(_projects)
                ? Directory.EnumerateDirectories(_projects)
                    .Select(project => Path.Combine(project, sessionId, "subagents", "workflows"))
                    .FirstOrDefault(Directory.Exists)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static FileInfo? Journal(string runDirectory)
    {
        var journal = new FileInfo(Path.Combine(runDirectory, "journal.jsonl"));

        return journal.Exists ? journal : null;
    }

    private static WorkflowActivity? Count(FileInfo journal)
    {
        var started = 0;
        var finished = 0;

        try
        {
            using var stream = new FileStream(journal.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                if (line.Contains(StartedMarker, StringComparison.Ordinal))
                {
                    started++;
                }
                else if (line.Contains(ResultMarker, StringComparison.Ordinal))
                {
                    finished++;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not count the workflow journal {Path}: {Reason}",
                journal.FullName, exception.Message);

            return null;
        }

        if (started == 0)
        {
            return null;
        }

        var runId = Path.GetFileName(Path.GetDirectoryName(journal.FullName))!;

        return new WorkflowActivity(runId, started, finished, journal.LastWriteTimeUtc, HasSummary(journal, runId));
    }

    /// <summary>
    /// Whether the run wrote the summary it writes when it ends. The summary sits beside the
    /// session's transcript rather than with the journal, so a finished run is legible from either.
    /// </summary>
    private static bool HasSummary(FileInfo journal, string runId)
    {
        var session = journal.Directory?.Parent?.Parent?.Parent;

        return session is not null && File.Exists(Path.Combine(session.FullName, "workflows", $"{runId}.json"));
    }
}
