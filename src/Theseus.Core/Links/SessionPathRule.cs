using Theseus.Core.Domain;
using Theseus.Core.Stopping;

namespace Theseus.Core.Links;

/// <summary>
/// The paths a session may not name as a brief, an output or a report: any card's handoff, stored or
/// worked out (old cards hold wrong stored paths, so both count), a <c>SESSION-STATUS-*.md</c> at a
/// working directory's root, the plan directory and Theseus's own data. A child pointing its report at
/// a handoff would otherwise pass the document check with a file it writes anyway.
/// </summary>
public sealed class SessionPathRule
{
    private readonly string _planDirectory;
    private readonly string _dataDirectory;

    /// <summary>Creates the rule for this board's plan directory and Theseus's data directory.</summary>
    public SessionPathRule(string planDirectory, string dataDirectory)
    {
        _planDirectory = planDirectory;
        _dataDirectory = dataDirectory;
    }

    /// <summary>Why a session may not name this path, or null when it may.</summary>
    public string? Refusal(string path, BoardSnapshot board)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (Full(path) is not { } full)
        {
            return $"{path} is not a usable path.";
        }

        if (Inside(full, _planDirectory))
        {
            return $"{path} is in the plan directory, which only the board writes.";
        }

        if (Inside(full, _dataDirectory))
        {
            return $"{path} is in Theseus's own data, which only Theseus writes.";
        }

        foreach (var card in board.Cards)
        {
            var handoffs = new[]
            {
                card.Handoff,
                card.Workstream.StatusDocumentPath,
                HandoffPath.For(card.Workstream with { StatusDocumentPath = null }),
            };

            if (handoffs.Any(handoff => Same(full, handoff)))
            {
                return $"{path} is the handoff of {card.Title} ({card.Key}). A handoff is never a brief or a report.";
            }

            if (Name(full).StartsWith("SESSION-STATUS-", StringComparison.OrdinalIgnoreCase)
                && full.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                && Same(Path.GetDirectoryName(full), card.Workstream.WorkingDirectory))
            {
                return $"{path} is a session status document at the root of {card.Workstream.WorkingDirectory}.";
            }
        }

        return null;
    }

    private static bool Inside(string full, string directory) =>
        Full(directory) is { } root
        && (Same(full, root)
            || full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    private static bool Same(string? full, string? other) =>
        full is not null && Full(other) is { } path && string.Equals(full, path, StringComparison.OrdinalIgnoreCase);

    private static string Name(string full) => Path.GetFileName(full);

    private static string? Full(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
