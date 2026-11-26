using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Theseus.Core.Stopping;

/// <summary>
/// Whether a session's turn ended on a network or server error that a later try can get past (2.4 W2): Claude Code
/// retries inside a turn but not once it has ended, and on 2026-11-25 seven cards sat dead for half an hour each after a
/// DNS outage. It marks such an ending with <c>isApiErrorMessage</c> and text that starts "API Error:". A sign-in, a usage
/// limit, a payment or a refused request is never one: trying again cannot fix those, and only the owner can.
/// </summary>
public static partial class TransportError
{
    /// <summary>
    /// When the turn ended on a transport error, or null: only when the last message of the session's own conversation, a
    /// prompt or a reply, is that error. Claude Code's own system records after it are passed over.
    /// </summary>
    /// <param name="lines">The end of the transcript as text.</param>
    public static DateTimeOffset? EndedOn(string? lines)
    {
        if (lines is null)
        {
            return null;
        }

        foreach (var line in lines.Split('\n').Reverse())
        {
            if (Last(line) is { } last)
            {
                return last.IsTransport ? last.AtUtc : null;
            }
        }

        return null;
    }

    /// <summary>Whether an API error's text says the network or the server failed, rather than a sign-in or a limit.</summary>
    public static bool IsTransport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.StartsWith("API Error:", StringComparison.Ordinal) && Transport().IsMatch(text) && !Owners().IsMatch(text);
    }

    private static (bool IsTransport, DateTimeOffset AtUtc)? Last(string line)
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
                || !root.TryGetProperty("type", out var type) || type.GetString() is not ("user" or "assistant")
                || (root.TryGetProperty("isSidechain", out var sidechain) && sidechain.ValueKind == JsonValueKind.True))
            {
                return null;
            }

            var at = root.TryGetProperty("timestamp", out var stamp) && stamp.ValueKind == JsonValueKind.String
                     && DateTimeOffset.TryParse(stamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;
            var isError = type.GetString() == "assistant"
                          && root.TryGetProperty("isApiErrorMessage", out var flag) && flag.ValueKind == JsonValueKind.True;

            return (isError && Text(root) is { } text && IsTransport(text), at);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root) =>
        root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object
        && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array
            ? string.Join('\n', content.EnumerateArray()
                .Where(block => block.ValueKind == JsonValueKind.Object && block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                .Select(block => block.GetProperty("text").GetString()))
            : null;

    // The texts measured in this machine's transcripts, 2026-11-26: can't reach the server, connection lost or refused,
    // overloaded, and the 5xx family; a timeout and a reset socket are the same failure.
    [GeneratedRegex(@"can't reach|connection (lost|refused|error|reset)|overloaded|timed out|timeout|\b5\d\d\b|ENOTFOUND|ECONNRESET|ETIMEDOUT|socket",
        RegexOptions.IgnoreCase)]
    private static partial Regex Transport();

    [GeneratedRegex(@"log ?in|sign.?in|auth|\b40[13]\b|\b429\b|credit|limit|subscription|payment|billing|forbidden",
        RegexOptions.IgnoreCase)]
    private static partial Regex Owners();
}
