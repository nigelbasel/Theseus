using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>piece-ids.json</c>: a stable id for every piece of work, stamped on every card of it (7.3). Historic time joined to
/// projects only through today's membership, so a re-slugged, moved or archived card landed in the wrong place; and one
/// card per piece undercounted, since 44 of 75 ledger runs were verifier cards. The id is Theseus's own, kept here and on
/// each entry of <c>project-members.json</c>, never in project.md (Nigel, 2026-11-27).
///
/// A piece is (project, slug) on first sight. When a card's slug disappears from its project while a new slug appears
/// for the same card, that is a rename and the new slug keeps the id; when the old slug is still there, the new one is a
/// second piece. A verifier takes the id of the card it reports back to, and a hand-over child in no project row takes its
/// parent's. Safe to call from any thread.
/// </summary>
public sealed class PieceIdBook
{
    private readonly string _filePath;
    private readonly Func<string> _newId;
    private readonly Lock _gate = new();
    private readonly List<PieceIdEntry> _pieces;

    /// <summary>Opens the book over a file.</summary>
    /// <param name="filePath">Where it is kept.</param>
    /// <param name="newId">Makes a new id; a short random one when null.</param>
    public PieceIdBook(string filePath, Func<string>? newId = null)
    {
        _filePath = filePath;
        _newId = newId ?? (() => Guid.NewGuid().ToString("N")[..12]);
        _pieces = Load(filePath);
    }

    /// <summary>Every piece known.</summary>
    public IReadOnlyList<PieceIdEntry> Pieces
    {
        get
        {
            lock (_gate)
            {
                return [.. _pieces];
            }
        }
    }

    /// <summary>Gives an id to every piece the projects name that has none, following renames; saves when anything changed.</summary>
    public void Update(IReadOnlyList<ProjectFileReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        lock (_gate)
        {
            var changed = false;

            foreach (var file in readings.Where(reading => reading.IsReadable).Select(reading => reading.File!))
            {
                var rows = file.Workstreams.Where(row => !string.IsNullOrWhiteSpace(row.Slug)).ToList();
                var slugs = rows.Select(row => row.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var card in rows.GroupBy(row => row.Workstream, StringComparer.OrdinalIgnoreCase))
                {
                    var gone = _pieces.Where(piece => Same(piece.Project, file.Name) && piece.Cards.Contains(card.Key, StringComparer.OrdinalIgnoreCase)
                                                      && !slugs.Contains(piece.Slug)).ToList();

                    foreach (var row in card)
                    {
                        if (Find(file.Name, row.Slug) is { } known)
                        {
                            changed |= AddCard(known, card.Key);

                            continue;
                        }

                        if (gone.Count > 0)
                        {
                            var renamed = gone[0];
                            gone.RemoveAt(0);
                            Replace(renamed, renamed with { Slug = row.Slug });
                            TheseusLog.Information("Piece {Id} in {Project} renamed from {Old} to {New}", renamed.Id, file.Name, renamed.Slug, row.Slug);
                        }
                        else
                        {
                            _pieces.Add(new PieceIdEntry(_newId(), file.Name, row.Slug, [card.Key]));
                        }

                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Save();
            }
        }
    }

    /// <summary>The id of a piece by its project and slug, or null.</summary>
    public string? IdFor(string project, string slug)
    {
        lock (_gate)
        {
            return Find(project, slug)?.Id;
        }
    }

    /// <summary>
    /// The id a card's time goes to: its own piece's; for a verifier, the piece of the card it checks; for a hand-over
    /// child in no row, its parent's. Null for a card on no piece, such as a scenario writer, which serves a whole project.
    /// A card found this way is remembered on the piece, so it keeps the id after its link goes.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="memberOf">A card's project and slug, by key.</param>
    /// <param name="links">The board's links.</param>
    public string? IdOf(Workstream card, Func<string, ProjectMember?> memberOf, IReadOnlyList<Link> links)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(memberOf);
        ArgumentNullException.ThrowIfNull(links);

        var subject = card.Role switch
        {
            WorkstreamRole.Verifier => LatestTo(card.Key, LinkKind.ReportsBack, links, from: true),
            WorkstreamRole.ScenarioWriter => null,
            _ => card.Key,
        };

        if (subject is null)
        {
            return null;
        }

        lock (_gate)
        {
            if (memberOf(subject) is { } member && Find(member.Project, member.Slug) is { } piece)
            {
                if (AddCard(piece, card.Key))
                {
                    Save();
                }

                return piece.Id;
            }

            if (_pieces.FirstOrDefault(each => each.Cards.Contains(card.Key, StringComparer.OrdinalIgnoreCase)) is { } remembered)
            {
                return remembered.Id;
            }

            if (card.Role == WorkstreamRole.None && LatestTo(card.Key, LinkKind.HandsOver, links, from: false) is { } parent
                && memberOf(parent) is { } parentMember && Find(parentMember.Project, parentMember.Slug) is { } parentPiece)
            {
                if (AddCard(parentPiece, card.Key))
                {
                    Save();
                }

                return parentPiece.Id;
            }

            return null;
        }
    }

    // A verifier reports back to the card it checks; a hand-over child is the To of its parent's link.
    private static string? LatestTo(string card, LinkKind kind, IReadOnlyList<Link> links, bool from) =>
        links.Where(link => link.IsActive && link.Kind == kind && Same(from ? link.From : link.To, card))
            .OrderBy(link => link.CreatedUtc)
            .Select(link => from ? link.To : link.From)
            .LastOrDefault();

    private PieceIdEntry? Find(string project, string slug) =>
        _pieces.FirstOrDefault(piece => Same(piece.Project, project) && Same(piece.Slug, slug));

    private bool AddCard(PieceIdEntry piece, string card)
    {
        if (piece.Cards.Contains(card, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        Replace(piece, piece with { Cards = [.. piece.Cards, card] });

        return true;
    }

    private void Replace(PieceIdEntry old, PieceIdEntry updated) => _pieces[_pieces.IndexOf(old)] = updated;

    private void Save()
    {
        try
        {
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_pieces, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the piece ids to {Path}", _filePath);
        }
    }

    private static List<PieceIdEntry> Load(string filePath)
    {
        var text = AtomicFile.TryReadAllText(filePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PieceIdEntry>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The piece ids in {Path} could not be read; ids will be given again", filePath);

            return [];
        }
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
