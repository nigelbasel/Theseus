using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Questions;

/// <summary>
/// The prompts sessions are stopped at, one file per session, written by the hook on Claude Code's Notification event and
/// cleared when the owner next types or the turn ends. The file says only what is asked; whether the session is still
/// stopped is the registry's "waiting", read beside it. Failures are logged and swallowed, as in
/// <see cref="OwnerQuestionStore"/>.
/// </summary>
public sealed class PermissionPromptStore
{
    /// <summary>How long a prompt is kept while its session is not waiting, for a registry that has not caught up.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    private readonly string _root;

    /// <summary>Creates the store over the folder that holds the prompts.</summary>
    public PermissionPromptStore(string root) => _root = root;

    /// <summary>The folder both the board and the hook use, beside the questions.</summary>
    public static string DefaultRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", "permission-prompts");

    /// <summary>Records the prompt, replacing any earlier one.</summary>
    public void Write(PermissionPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (PathFor(prompt.SessionId) is not { } path)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(path, JsonSerializer.Serialize(prompt, TheseusJson.Compact));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning("A prompt could not be written to {Path}: {Reason}", path, exception.Message);
        }
    }

    /// <summary>The session's prompt, or null.</summary>
    public PermissionPrompt? Read(string? sessionId)
    {
        if (PathFor(sessionId) is not { } path || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PermissionPrompt>(File.ReadAllText(path), TheseusJson.Compact);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TheseusLog.Debug("A prompt could not be read from {Path}: {Reason}", path, exception.Message);

            return null;
        }
    }

    /// <summary>Clears the session's prompt: it was answered.</summary>
    public void Clear(string? sessionId) => Delete(PathFor(sessionId));

    /// <summary>
    /// Removes the prompt of every session not waiting now, once older than <see cref="Grace"/>: the hook writes it a moment
    /// before the registry says "waiting", and a prompt answered leaves the session working with no hook event to clear it.
    /// </summary>
    public void RemoveExcept(IEnumerable<string> waitingSessionIds, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(waitingSessionIds);

        if (!Directory.Exists(_root))
        {
            return;
        }

        var waiting = new HashSet<string>(waitingSessionIds, StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var path in Directory.GetFiles(_root, "*.json"))
            {
                if (!waiting.Contains(Path.GetFileNameWithoutExtension(path))
                    && File.GetLastWriteTimeUtc(path) < (nowUtc - Grace).UtcDateTime)
                {
                    Delete(path);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("The prompts folder could not be swept: {Reason}", exception.Message);
        }
    }

    // As in OwnerQuestionStore: the identifier comes from a hook's standard input, so it becomes a path only as a session id.
    private string? PathFor(string? sessionId) =>
        Guid.TryParseExact(sessionId, "D", out var id) ? Path.Combine(_root, $"{id:D}.json") : null;

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
            TheseusLog.Debug("A prompt could not be removed from {Path}: {Reason}", path, exception.Message);
        }
    }
}
