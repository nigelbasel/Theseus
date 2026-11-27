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
    /// <summary>How many sweeps in a row a session must be missing from before its question goes.</summary>
    public const int SweepsMissing = 3;

    /// <summary>How long an answer waits to be delivered, as long as the tell that carries it.</summary>
    public static readonly TimeSpan AnswerKept = TimeSpan.FromDays(7);

    private readonly string _root;
    private readonly Dictionary<string, int> _missing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the store over the folder that holds the questions.</summary>
    public OwnerQuestionStore(string root) => _root = root;

    /// <summary>
    /// Records a question the session asked outright, replacing any question or guess it had open. False when it could
    /// not be written, so the session is not told the owner can see it.
    /// </summary>
    public bool Ask(OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return Write(question with { IsGuess = false });
    }

    /// <summary>Records a guess, unless the session has a question of its own open, which a guess never replaces.</summary>
    public void Guess(OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (Read(question.SessionId) is { IsGuess: false })
        {
            return;
        }

        _ = Write(question with { IsGuess = true });
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

    /// <summary>
    /// Records the owner's answer on the session's open question, so the wait says it is answered until the answer is
    /// delivered (2.4e). The question as answered, or null when there was none open or it could not be written.
    /// </summary>
    public OwnerQuestion? RecordAnswer(string? sessionId, string answer, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);

        if (Read(sessionId) is not { IsOpen: true } open)
        {
            return null;
        }

        var answered = open with { Answer = answer, AnsweredUtc = now };

        return Write(answered) ? answered : null;
    }

    /// <summary>Clears the session's question only if it was answered on the board: its answer has been delivered.</summary>
    public void ClearAnswered(string? sessionId)
    {
        if (Read(sessionId) is { IsOpen: false })
        {
            Clear(sessionId);
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
    /// may leave a guess for any of them; this is what keeps the folder from growing (spec item 10). A file goes only once
    /// its session has been missing from <see cref="SweepsMissing"/> sweeps in a row and it is older than
    /// <paramref name="writtenBeforeUtc"/>, so one registry read that comes back short, or a session not in it yet, never
    /// loses a question the owner has still to answer. A question a session asked outright stays while its session is in
    /// <paramref name="keepAsked"/> (the cards' last sessions), and an answer stays until it is delivered, so a headless
    /// session between runs or a stopped one can still be answered on the board (2.4e).
    /// </summary>
    public void RemoveExcept(IEnumerable<string> runningSessionIds, DateTimeOffset writtenBeforeUtc, IEnumerable<string>? keepAsked = null)
    {
        ArgumentNullException.ThrowIfNull(runningSessionIds);

        if (!Directory.Exists(_root))
        {
            return;
        }

        var running = new HashSet<string>(runningSessionIds, StringComparer.OrdinalIgnoreCase);
        var kept = new HashSet<string>(keepAsked ?? [], StringComparer.OrdinalIgnoreCase);

        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in Directory.GetFiles(_root, "*.json"))
            {
                var session = Path.GetFileNameWithoutExtension(path);
                seen.Add(session);

                if (running.Contains(session) || (Read(session) is { } held && (held.AnsweredUtc > writtenBeforeUtc - AnswerKept || (held is { IsOpen: true, IsGuess: false } && kept.Contains(session)))))
                {
                    _missing.Remove(session);

                    continue;
                }

                var missing = _missing[session] = _missing.GetValueOrDefault(session) + 1;

                if (missing >= SweepsMissing && File.GetLastWriteTimeUtc(path) < writtenBeforeUtc.UtcDateTime)
                {
                    Delete(path);
                    _missing.Remove(session);
                }
            }

            foreach (var gone in _missing.Keys.Where(session => !seen.Contains(session)).ToList())
            {
                _missing.Remove(gone);
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

    private bool Write(OwnerQuestion question)
    {
        if (PathFor(question.SessionId) is not { } path)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(path, JsonSerializer.Serialize(question, TheseusJson.Compact));

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning("A question could not be written to {Path}: {Reason}", path, exception.Message);

            return false;
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
