using System.Text;
using System.Text.Json;

namespace Theseus.Core.Questions;

/// <summary>
/// The last thing the session said in its transcript, read from the file's end only: the hook runs on every turn of
/// every session on the machine, and a day-long transcript runs to many megabytes.
/// </summary>
public static class TranscriptTail
{
    /// <summary>How much of the file's end is read. A turn's final message is far shorter than this.</summary>
    public const int Window = 256 * 1024;

    /// <summary>
    /// The text of the last assistant message, or null when the turn ended on something else (a tool call, the
    /// question tool among them), the file cannot be read, or nothing in the window is an assistant message.
    /// Subagents' records are passed over: their words are not the session's.
    /// </summary>
    public static string? LastAssistantText(string? transcriptPath)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath))
        {
            return null;
        }

        try
        {
            return LastAssistantTextIn(ReadEnd(transcriptPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The same, over transcript lines already read.</summary>
    public static string? LastAssistantTextIn(string lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines.Split('\n').Reverse())
        {
            if (Block(line) is { } block)
            {
                return block.Type == "text" ? block.Text : null;
            }
        }

        return null;
    }

    private static (string Type, string? Text)? Block(string line)
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
                || !root.TryGetProperty("type", out var type) || type.GetString() != "assistant"
                || (root.TryGetProperty("isSidechain", out var sidechain) && sidechain.ValueKind == JsonValueKind.True)
                || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array
                || content.GetArrayLength() == 0)
            {
                return null;
            }

            var last = content[content.GetArrayLength() - 1];
            var kind = last.TryGetProperty("type", out var kindElement) ? kindElement.GetString() ?? string.Empty : string.Empty;
            var text = last.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String
                ? textElement.GetString()
                : null;

            return (kind, text);
        }
        catch (JsonException)
        {
            // The window's first line is usually cut part-way; any line that is not whole JSON is not a record.
            return null;
        }
    }

    private static string ReadEnd(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = (int)Math.Min(stream.Length, Window);
        stream.Seek(-length, SeekOrigin.End);
        var buffer = new byte[length];
        stream.ReadExactly(buffer);

        return Encoding.UTF8.GetString(buffer);
    }
}
