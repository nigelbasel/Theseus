using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Theseus.Core.Questions;

/// <summary>
/// What a session's transcript ends with, read from the file's end only: the hook runs on every turn of every session
/// on the machine, and a day-long transcript runs to many megabytes.
/// </summary>
public static class TranscriptTail
{
    /// <summary>How much of the file's end is read. A turn's final message is far shorter than this.</summary>
    public const int Window = 256 * 1024;

    private const string QuestionTool = "AskUserQuestion";

    /// <summary>The last <see cref="Window"/> bytes of the transcript as text, or null when it cannot be read.</summary>
    public static string? Read(string? transcriptPath)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = (int)Math.Min(stream.Length, Window);
            stream.Seek(-length, SeekOrigin.End);
            var buffer = new byte[length];
            stream.ReadExactly(buffer);

            return Encoding.UTF8.GetString(buffer);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The text of the last assistant message, or null when the turn ended on something else (a tool call, the
    /// question tool among them) or nothing in the window is an assistant message. Subagents' records are passed over:
    /// their words are not the session's.
    /// </summary>
    public static string? LastAssistantTextIn(string? lines)
    {
        foreach (var record in Records(lines).Reverse())
        {
            if (record.Type == "assistant" && LastBlock(record.Content) is { } block)
            {
                return block.TryGetProperty("type", out var type) && type.GetString() == "text"
                       && block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
                    ? text.GetString()
                    : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the session's question tool was answered at or after <paramref name="sinceUtc"/>. Answering it sends no
    /// prompt, so without this a question asked with ask_owner and answered in the tab would stand until the owner next
    /// typed, should the session forget to say it has its answer.
    /// </summary>
    public static bool QuestionToolAnsweredSince(string? lines, DateTimeOffset sinceUtc)
    {
        var asks = new HashSet<string>(StringComparer.Ordinal);

        foreach (var record in Records(lines))
        {
            foreach (var block in Blocks(record.Content))
            {
                if (record.Type == "assistant" && Text(block, "type") == "tool_use" && Text(block, "name") == QuestionTool
                    && Text(block, "id") is { } id)
                {
                    asks.Add(id);
                }
                else if (record.Type == "user" && Text(block, "type") == "tool_result" && Text(block, "tool_use_id") is { } answered
                         && asks.Contains(answered) && record.Timestamp >= sinceUtc)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The names of the tools the session called in its current turn: since the last prompt typed or sent to it, not since
    /// the last tool result, which is the same turn going on. Subagents' calls are passed over. A turn longer than the
    /// window gives every call in the window, so a refusal leans towards letting the turn end (2.4 W2).
    /// </summary>
    public static IReadOnlyList<string> ToolsCalledThisTurn(string? lines)
    {
        var called = new List<string>();

        if (lines is null)
        {
            return called;
        }

        foreach (var line in lines.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object
                    || (root.TryGetProperty("isSidechain", out var sidechain) && sidechain.ValueKind == JsonValueKind.True)
                    || (root.TryGetProperty("isMeta", out var meta) && meta.ValueKind == JsonValueKind.True)
                    || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("content", out var content))
                {
                    continue;
                }

                var type = Text(root, "type");

                if (type == "user" && IsPrompt(content))
                {
                    called.Clear();
                }
                else if (type == "assistant" && content.ValueKind == JsonValueKind.Array)
                {
                    called.AddRange(Blocks(content).Where(block => Text(block, "type") == "tool_use")
                        .Select(block => Text(block, "name")).OfType<string>());
                }
            }
            catch (JsonException)
            {
                // The window's first line is usually cut part-way.
            }
        }

        return called;
    }

    // A prompt is text the session was sent; a user record holding any tool result is the turn going on, whatever else it
    // carries, and Claude Code's own injected records (isMeta) are passed over above.
    private static bool IsPrompt(JsonElement content) =>
        content.ValueKind == JsonValueKind.String
        || (content.ValueKind == JsonValueKind.Array && Blocks(content).Any(block => Text(block, "type") == "text")
            && Blocks(content).All(block => Text(block, "type") != "tool_result"));

    private static IEnumerable<Record> Records(string? lines)
    {
        if (lines is null)
        {
            yield break;
        }

        foreach (var line in lines.Split('\n'))
        {
            if (Parse(line) is { } record)
            {
                yield return record;
            }
        }
    }

    private static Record? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "type") is not { } type
                || (root.TryGetProperty("isSidechain", out var sidechain) && sidechain.ValueKind == JsonValueKind.True)
                || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var timestamp = Text(root, "timestamp") is { } stamp
                            && DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;

            return new Record(type, content.Clone(), timestamp);
        }
        catch (JsonException)
        {
            // The window's first line is usually cut part-way; any line that is not whole JSON is not a record.
            return null;
        }
    }

    private static JsonElement? LastBlock(JsonElement content) =>
        content.GetArrayLength() == 0 ? null : content[content.GetArrayLength() - 1];

    private static IEnumerable<JsonElement> Blocks(JsonElement content) =>
        content.EnumerateArray().Where(block => block.ValueKind == JsonValueKind.Object);

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private readonly record struct Record(string Type, JsonElement Content, DateTimeOffset Timestamp);
}
