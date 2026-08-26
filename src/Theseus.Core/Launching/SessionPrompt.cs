namespace Theseus.Core.Launching;

/// <summary>
/// Joins the standing preamble to a workstream's own opening prompt. The preamble comes first
/// because it frames what follows, and it is composed at launch rather than written into the plan
/// files, so changing it changes every future session without rewriting anything a session authored.
/// </summary>
public static class SessionPrompt
{
    /// <summary>The prompt a session actually opens with.</summary>
    public static string Compose(string? preamble, string? prompt)
    {
        var standing = preamble?.Trim() ?? string.Empty;
        var own = prompt?.Trim() ?? string.Empty;

        if (standing.Length == 0)
        {
            return own;
        }

        if (own.Length == 0)
        {
            return standing;
        }

        return $"{standing}{Environment.NewLine}{Environment.NewLine}{own}";
    }
}
