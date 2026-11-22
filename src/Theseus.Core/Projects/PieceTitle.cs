namespace Theseus.Core.Projects;

/// <summary>What a piece's box is titled: what the piece achieves, never its records folder's name (1.1f A4, D2).</summary>
public static class PieceTitle
{
    private static readonly string[] Separators = [" - ", ": "];

    /// <summary>
    /// Its row's What cell; else its objective's title when the objective has one piece; else its card's name after the
    /// last " - " (or ": "); else the objective's title. A piece no project file lists takes its card's name the same way.
    /// </summary>
    /// <param name="what">Its row's What cell, or null.</param>
    /// <param name="objectiveTitle">Its objective's title, or null when no project file lists it.</param>
    /// <param name="piecesInObjective">How many pieces its objective has.</param>
    /// <param name="cardName">The name of the card that works it.</param>
    public static string For(string? what, string? objectiveTitle, int piecesInObjective, string cardName)
    {
        ArgumentNullException.ThrowIfNull(cardName);

        if (!string.IsNullOrWhiteSpace(what))
        {
            return what.Trim();
        }

        if (objectiveTitle is not null && piecesInObjective <= 1)
        {
            return objectiveTitle;
        }

        return AfterSeparator(cardName) ?? objectiveTitle ?? cardName;
    }

    private static string? AfterSeparator(string name)
    {
        foreach (var separator in Separators)
        {
            var at = name.LastIndexOf(separator, StringComparison.Ordinal);

            if (at >= 0 && name[(at + separator.Length)..].Trim() is { Length: > 0 } rest)
            {
                return rest;
            }
        }

        return null;
    }
}
