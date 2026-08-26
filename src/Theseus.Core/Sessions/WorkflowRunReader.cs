using System.Text.Json;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <summary>
/// Reads the record Claude Code keeps of each workflow a session ran, from the session's own folder
/// under the projects directory. The folder is found by looking for the session under each project
/// rather than by deriving the project's name from a working directory, because the name is a
/// mangling of the path the session started in and a session outlives the directory it began in.
/// </summary>
public sealed class WorkflowRunReader : IWorkflowRunReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _projects;

    /// <summary>Creates a reader over the projects directory.</summary>
    public WorkflowRunReader(string projects) => _projects = projects;

    /// <summary>Where Claude Code keeps one folder per project.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <inheritdoc />
    public IReadOnlyList<WorkflowRun> Read(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !Directory.Exists(_projects))
        {
            return [];
        }

        if (Locate(sessionId) is not { } directory)
        {
            return [];
        }

        try
        {
            return
            [
                .. Directory.EnumerateFiles(directory, "wf_*.json")
                    .Select(Parse)
                    .OfType<WorkflowRun>()
                    .OrderByDescending(run => run.Timestamp ?? DateTimeOffset.MinValue)
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not read the workflows at {Directory}: {Reason}", directory, exception.Message);

            return [];
        }
    }

    private string? Locate(string sessionId)
    {
        try
        {
            return Directory.EnumerateDirectories(_projects)
                .Select(project => Path.Combine(project, sessionId, "workflows"))
                .FirstOrDefault(Directory.Exists);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not look for session {SessionId}: {Reason}", sessionId, exception.Message);

            return null;
        }
    }

    private static WorkflowRun? Parse(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkflowRun>(File.ReadAllText(path), Options)
                   is { } run && (run.RunId is not null || run.WorkflowName is not null)
                ? run with { Phases = run.Phases ?? [] }
                : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Skipped the workflow record at {Path}: {Reason}", path, exception.Message);

            return null;
        }
    }
}
