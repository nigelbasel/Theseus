namespace Theseus.Core.Projects;

/// <summary>
/// What a project's Stop asks (1.1d AC 43): each of its cards with a session open writes its handover and stops, as the
/// card's own Stop asks it to.
/// </summary>
public sealed class ProjectStop
{
    private ProjectStop(string title, IReadOnlyList<string> keys, IReadOnlyList<string> titles)
    {
        Title = title;
        Keys = keys;
        Titles = titles;
    }

    /// <summary>The project's title.</summary>
    public string Title { get; }

    /// <summary>The keys of its cards with a session open, in file order.</summary>
    public IReadOnlyList<string> Keys { get; }

    /// <summary>Their names on screen.</summary>
    public IReadOnlyList<string> Titles { get; }

    /// <summary>Whether any of its cards has a session to stop.</summary>
    public bool CanStop => Keys.Count > 0;

    /// <summary>What Stop does, or why it cannot.</summary>
    public string ToolTip => CanStop
        ? $"Stop {Title}: ask {string.Join(", ", Titles)} to write a handover and exit"
        : $"No card of {Title} is running";

    /// <summary>The Stop of a project, from its pieces of work.</summary>
    public static ProjectStop For(string title, IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(pieces);

        var live = pieces
            .Where(piece => piece.IsCurrentPiece && piece.Card is { HasSession: true })
            .Select(piece => piece.Card!)
            .DistinctBy(card => card.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProjectStop(title, [.. live.Select(card => card.Key)], [.. live.Select(card => card.Title)]);
    }
}
