using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Questions;

/// <summary>
/// The open questions, one file per session, written by <c>ask_owner</c> and the stop hook, cleared by the hook when
/// the owner next types in that tab. The hook runs inside every session on the machine, so a failure here is logged
/// and swallowed: a session must never learn from its own hooks that Theseus is installed badly.
/// </summary>
public sealed class OwnerQuestionStore
{
    private readonly string _root;

    /// <summary>Creates the store over the folder that holds the questions.</summary>
    public OwnerQuestionStore(string root) => _root = root;

    /// <summary>Records a question the session asked outright, replacing any question or guess it had open.</summary>
    public void Ask(OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        Write(question with { IsGuess = false });
    }

    /// <summary>Records a guess, unless the session has a question of its own open, which a guess never replaces.</summary>
    public void Guess(OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (Read(question.SessionId) is { IsGuess: false })
        {
            return;
        }

        Write(question with { IsGuess = true });
    }

    /// <summary>The session's open question, or null.</summary>
    public OwnerQuestion? Read(string? sessionId)
    {
        if (PathFor(sessionId) is not { } path || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OwnerQuestion>(File.ReadAllText(path), TheseusJson.Compact);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TheseusLog.Debug("A question could not be read from {Path}: {Reason}", path, exception.Message);

            return null;
        }
    }

    /// <summary>Clears whatever the session had open: the owner answered.</summary>
    public void Clear(string? sessionId) => Delete(PathFor(sessionId));

    /// <summary>Clears a guess and leaves an asked question: a turn ended without asking, but the question stands.</summary>
    public void ClearGuess(string? sessionId)
    {
        if (Read(sessionId) is { IsGuess: true })
        {
            Clear(sessionId);
        }
    }

    /// <summary>
    /// Removes the questions of every session not running. The hook cannot tell which sessions Theseus launched, so it
    /// may leave a guess for any of them; this is what keeps the folder from growing (spec item 10). Only files older
    /// than <paramref name="writtenBeforeUtc"/> go, so a registry read that comes back short, or a session not yet in it,
    /// does not lose a question just asked.
    /// </summary>
    public void RemoveExcept(IEnumerable<string> runningSessionIds, DateTimeOffset writtenBeforeUtc)
    {
        ArgumentNullException.ThrowIfNull(runningSessionIds);

        if (!Directory.Exists(_root))
        {
            return;
        }

        var running = new HashSet<string>(runningSessionIds, StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var path in Directory.GetFiles(_root, "*.json"))
            {
                if (!running.Contains(Path.GetFileNameWithoutExtension(path)) && File.GetLastWriteTimeUtc(path) < writtenBeforeUtc.UtcDateTime)
                {
                    Delete(path);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("The questions folder could not be swept: {Reason}", exception.Message);
        }
    }

    // The identifier arrives from a hook's standard input or a request's path, so it becomes a path only once it parses
    // as a session id, as StopRequestPath does.
    private string? PathFor(string? sessionId) =>
        Guid.TryParseExact(sessionId, "D", out var id) ? Path.Combine(_root, $"{id:D}.json") : null;

    private void Write(OwnerQuestion question)
    {
        if (PathFor(question.SessionId) is not { } path)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(path, JsonSerializer.Serialize(question, TheseusJson.Compact));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning("A question could not be written to {Path}: {Reason}", path, exception.Message);
        }
    }

    private static void Delete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("A question could not be removed from {Path}: {Reason}", path, exception.Message);
        }
    }
}
