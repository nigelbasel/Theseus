using System.Text.Json;

namespace Theseus.Core.Sessions;

/// <summary>
/// Which model a session is running, read from its transcript.
///
/// Nothing else says. The session registry has no model field, and a session's own idea of its
/// model is fixed when it starts — it does not learn that it was switched. The transcript records
/// the model on every assistant turn, so the last real turn is the truth, and a switch part-way
/// through shows up as the value changing.
/// </summary>
public static class ModelName
{
    /// <summary>The harness writes this on turns it generated itself; they say nothing about the model.</summary>
    private const string Synthetic = "<synthetic>";

    /// <summary>
    /// The model recorded on one transcript line, or null when the line is not a real assistant
    /// turn. Parsed rather than searched: a turn's text can itself contain the words
    /// <c>"model":"..."</c> — this one does — and a substring match would read that as the answer.
    /// </summary>
    public static string? FromTranscriptLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Contains("\"assistant\"", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.GetString() != "assistant"
                || !root.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("model", out var model))
            {
                return null;
            }

            var id = model.GetString();

            return string.IsNullOrWhiteSpace(id) || id == Synthetic ? null : id;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// What to call a model on a card: "Opus 5", "Fable 5.1", "Sonnet 5", "Haiku 4.5". An identifier
    /// this does not recognise is shown as it is rather than guessed at.
    /// </summary>
    public static string Friendly(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        var parts = id.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3 || !string.Equals(parts[0], "claude", StringComparison.OrdinalIgnoreCase))
        {
            return id;
        }

        var family = char.ToUpperInvariant(parts[1][0]) + parts[1][1..];
        var version = string.Join('.', parts.Skip(2).TakeWhile(part => part.Length <= 2 && part.All(char.IsDigit)));

        return version.Length == 0 ? id : $"{family} {version}";
    }
}
