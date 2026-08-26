using System.Text.Json;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <summary>
/// Reads a session's todo list from the directory Claude Code keeps it in, one file per item, named
/// by the session. Like the session registry, these files belong to Claude Code: they are read and
/// never written, and anything unreadable is skipped rather than allowed to stop the board.
/// </summary>
public sealed class SessionTodoReader : ISessionTodoReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _root;

    /// <summary>Creates a reader over the directory that holds one folder per session.</summary>
    public SessionTodoReader(string root) => _root = root;

    /// <summary>Where Claude Code keeps the lists.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "tasks");

    /// <inheritdoc />
    public IReadOnlyList<SessionTodo> Read(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return [];
        }

        var directory = Path.Combine(_root, sessionId);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return
            [
                .. Directory.EnumerateFiles(directory, "*.json")
                    .Select(Parse)
                    .OfType<SessionTodo>()
                    .OrderBy(Position)
                    .ThenBy(todo => todo.Id, StringComparer.OrdinalIgnoreCase)
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not read the todo list at {Directory}: {Reason}", directory, exception.Message);

            return [];
        }
    }

    private static SessionTodo? Parse(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<SessionTodo>(File.ReadAllText(path), Options);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Skipped the todo item at {Path}: {Reason}", path, exception.Message);

            return null;
        }
    }

    private static int Position(SessionTodo todo) =>
        int.TryParse(todo.Id, out var number) ? number : int.MaxValue;
}
