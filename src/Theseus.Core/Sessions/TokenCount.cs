namespace Theseus.Core.Sessions;

/// <summary>Writes token counts at the size a person reads them.</summary>
public static class TokenCount
{
    /// <summary>A count rounded to the unit that keeps it short: 2.2M, 431k, 900.</summary>
    public static string Format(long tokens) => tokens switch
    {
        >= 1_000_000 => $"{tokens / 1_000_000d:0.#}M",
        >= 1_000 => $"{tokens / 1_000d:0.#}k",
        _ => tokens.ToString(),
    };
}
