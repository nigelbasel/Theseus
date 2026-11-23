using System.Text.RegularExpressions;

namespace Theseus.Core.Questions;

/// <summary>
/// Whether a turn's last message probably asks the owner something, written only in prose, which leaves the session
/// "idle" and the question unseen on the board (1.6). The rule is G2 of the owner-questions research, measured on this
/// machine's 718 transcript endings: it caught 27 of 28 that asked and flagged 3 of 682 that did not. Its phrases are
/// that measurement's; change one only by measuring again.
/// </summary>
public static partial class ProseQuestion
{
    /// <summary>The longest line shown for a guess.</summary>
    public const int MaxLength = 200;

    /// <summary>
    /// The line that asks, cleaned of markdown and cut to <see cref="MaxLength"/>, or null when the message does not
    /// ask. Only its last two paragraphs are read: a question earlier in a long report is not what the turn ends on.
    /// </summary>
    public static string? AskingLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var paragraphs = Paragraphs(message);
        var tail = paragraphs.Skip(Math.Max(0, paragraphs.Count - 2)).SelectMany(Lines).ToList();
        var asking = tail.LastOrDefault(Asks);

        if (asking is null && !Phrase(string.Join('\n', tail)))
        {
            return null;
        }

        return Shown(asking ?? tail.LastOrDefault(line => Clean(line).Length > 0) ?? string.Empty);
    }

    private static bool Asks(string line)
    {
        var clean = Clean(line);

        return clean.EndsWith('?') || SentenceQuestion().IsMatch(Quoted().Replace(clean, "Q")) || Phrase(line);
    }

    private static bool Phrase(string text) => AskingPhrase().IsMatch(text) || Request().IsMatch(text);

    private static List<string> Paragraphs(string message)
    {
        var body = Fence().Replace(message.Replace("\r", string.Empty, StringComparison.Ordinal), string.Empty);

        return [.. ParagraphBreak().Split(body)
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => paragraph.Length > 0 && !Rule().IsMatch(paragraph))];
    }

    private static IEnumerable<string> Lines(string paragraph) => paragraph.Split('\n');

    private static string Clean(string line) => CodeOrUrl().Replace(line, string.Empty).Trim().TrimEnd('*', '_', '`', ' ').Trim();

    private static string Shown(string line)
    {
        var shown = ListMarker().Replace(line.Trim(), string.Empty).Replace("**", string.Empty, StringComparison.Ordinal).Trim();

        return shown.Length <= MaxLength ? shown : $"{shown[..(MaxLength - 1)]}…";
    }

    [GeneratedRegex("```.*?```", RegexOptions.Singleline)]
    private static partial Regex Fence();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"^[-*_]{3,}$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"`[^`]*`|https?://\S+")]
    private static partial Regex CodeOrUrl();

    [GeneratedRegex("\"[^\"\n]*\"|“[^”\n]*”")]
    private static partial Regex Quoted();

    // A "?" that ends a sentence inside a line: "…on the MacBooks? Enforced means…". Quoted text is replaced by Q
    // first, so a quoted button label such as "What's next? Instruct…" is not taken for a question.
    [GeneratedRegex(@"[A-Za-z)]\?['*)]*\s+[A-Z(Q]")]
    private static partial Regex SentenceQuestion();

    [GeneratedRegex(@"\b(shall i|should i|do you want|would you like|want me to|which (one|option|do you|would you)|can you confirm|please confirm|ok to|okay to|is that ok|are you happy|do you prefer)",
        RegexOptions.IgnoreCase)]
    private static partial Regex AskingPhrase();

    // Only at the start of a sentence, where these are requests: "I can't say whether" is not one.
    [GeneratedRegex(@"(^|[.!:]\s+)(give me the go-ahead|say the word|(say|tell me|let me know) (if|whether|which)|please confirm|confirm (and|that|whether|if)|if you (send|give|tell) me)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Request();

    [GeneratedRegex(@"^(\s*([-*+]|\d+[.)])\s+|#+\s+)")]
    private static partial Regex ListMarker();
}
