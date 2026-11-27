using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// Writes <c>piece-events.jsonl</c> (7.3): every now_at stage change including skips, every criterion tick with its time
/// and who made it, spec acceptance, and finish or abandon. Every writer goes through here so each line is stamped the same
/// way. Safe to call from the MCP server's threads.
/// </summary>
public sealed class PieceEvents
{
    private readonly JsonLinesFile<PieceEvent> _log;
    private readonly PieceIdBook _ids;
    private readonly Func<string, ProjectMember?> _memberOf;
    private readonly Func<string, string?> _pieceOf;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeZoneInfo _zone;
    private readonly Lock _gate = new();
    private HashSet<string>? _ended;
    private HashSet<string>? _archived;

    /// <summary>Creates the writer.</summary>
    /// <param name="log">The file.</param>
    /// <param name="ids">The pieces' ids.</param>
    /// <param name="memberOf">A card's project and slug, by key.</param>
    /// <param name="pieceOf">The piece id a card's work goes to, by key (<see cref="PieceIdBook.IdOf"/>).</param>
    /// <param name="clock">The time now.</param>
    /// <param name="zone">The zone whose offset is stamped; this machine's when null.</param>
    public PieceEvents(JsonLinesFile<PieceEvent> log, PieceIdBook ids, Func<string, ProjectMember?> memberOf, Func<string, string?> pieceOf,
        Func<DateTimeOffset> clock, TimeZoneInfo? zone = null)
    {
        _log = log;
        _ids = ids;
        _memberOf = memberOf;
        _pieceOf = pieceOf;
        _clock = clock;
        _zone = zone ?? TimeZoneInfo.Local;
    }

    /// <summary>Every event recorded.</summary>
    public IReadOnlyList<PieceEvent> Read() => _log.Read();

    /// <summary>A card's session entered a stage, or said it skipped one.</summary>
    public void Stage(string card, ProjectMember member, string stage, string detail, bool skipped)
    {
        ArgumentNullException.ThrowIfNull(member);

        Write(Event(skipped ? PieceEventKind.StageSkipped : PieceEventKind.Stage, member.Project, _pieceOf(card), member.Slug, card)
            with { Stage = stage, Detail = detail });
    }

    /// <summary>A criterion of an objective was ticked or unticked.</summary>
    /// <param name="project">The project.</param>
    /// <param name="objective">The objective's number.</param>
    /// <param name="index">The criterion's index, from 0.</param>
    /// <param name="ticked">Whether it was ticked.</param>
    /// <param name="byCard">The card of the session that did it, or null when the owner did.</param>
    public void Criterion(string project, string objective, int index, bool ticked, string? byCard)
    {
        var member = byCard is null ? null : _memberOf(byCard);

        Write(Event(ticked ? PieceEventKind.CriterionTicked : PieceEventKind.CriterionUnticked, project,
                byCard is null ? null : _pieceOf(byCard), member?.Slug, byCard ?? PieceEvent.Owner)
            with { Objective = objective, Criterion = index + 1 });
    }

    /// <summary>The owner accepted a piece's spec.</summary>
    public void SpecAccepted(string project, string slug) =>
        Write(Event(PieceEventKind.SpecAccepted, project, _ids.IdFor(project, slug), slug, PieceEvent.Owner));

    /// <summary>
    /// The owner confirmed the report on a link. It finishes the piece when it is the piece's verifier's or the piece's own
    /// report, with a verdict of done or go; otherwise it is recorded as confirmed only. Nothing is recorded for a card in
    /// no project.
    /// </summary>
    /// <param name="link">The link, as confirmed.</param>
    /// <param name="from">The card that reported, or null when it is not on the board.</param>
    public void ReportConfirmed(Link link, Workstream? from)
    {
        ArgumentNullException.ThrowIfNull(link);

        var pieceId = _pieceOf(link.From);
        var subject = from?.Role == WorkstreamRole.Verifier ? link.To : link.From;

        if ((_memberOf(subject) ?? _memberOf(link.From)) is not { } member)
        {
            return;
        }

        var verdict = link.LatestReport?.Verdict;
        var finishes = pieceId is not null && ReportVerdicts.ConfirmsInOnePress(verdict)
                       && link.Kind == LinkKind.ReportsBack && from?.Role is WorkstreamRole.Verifier or WorkstreamRole.None;

        Write(Event(finishes ? PieceEventKind.Finished : PieceEventKind.ReportConfirmed, member.Project, pieceId, member.Slug, PieceEvent.Owner)
            with { Verdict = verdict, Card = link.From });
    }

    /// <summary>
    /// Looks at the board's cards: a piece's own card archived since the last look, its piece not finished, records the
    /// piece as abandoned. The first look only learns which cards are archived already.
    /// </summary>
    public void Watch(IReadOnlyList<Workstream> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        List<PieceEvent> abandoned = [];

        lock (_gate)
        {
            var archived = cards.Where(card => card.IsArchived).Select(card => card.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (_archived is null)
            {
                _archived = archived;

                return;
            }

            _ended ??= [.. _log.Read().Where(each => each.Kind is PieceEventKind.Finished or PieceEventKind.Abandoned && each.PieceId is not null)
                .Select(each => each.PieceId!)];

            foreach (var card in cards.Where(card => card is { IsArchived: true, Role: WorkstreamRole.None } && !_archived.Contains(card.Key)))
            {
                if (_memberOf(card.Key) is { } member && _ids.IdFor(member.Project, member.Slug) is { } id && !_ended.Contains(id))
                {
                    abandoned.Add(Event(PieceEventKind.Abandoned, member.Project, id, member.Slug, PieceEvent.Owner) with { Card = card.Key });
                }
            }

            _archived = archived;
        }

        foreach (var each in abandoned)
        {
            Write(each);
        }
    }

    private PieceEvent Event(PieceEventKind kind, string project, string? pieceId, string? slug, string by)
    {
        var now = _clock();

        return new PieceEvent(now, kind, project, pieceId, slug, by) { Offset = AttentionOffset.Of(now, _zone) };
    }

    private void Write(PieceEvent pieceEvent)
    {
        lock (_gate)
        {
            if (pieceEvent is { Kind: PieceEventKind.Finished or PieceEventKind.Abandoned, PieceId: { } id })
            {
                _ended?.Add(id);
            }
        }

        _log.Append(pieceEvent);
    }
}
