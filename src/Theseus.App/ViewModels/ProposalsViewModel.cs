using System.Collections.ObjectModel;
using Theseus.Core.Proposals;

namespace Theseus.App.ViewModels;

/// <summary>
/// The workstreams sessions have proposed and Nigel has not yet decided, at the top of the Chains tab.
/// Nothing in here creates a card: Review opens the composer, and the card exists only when Nigel
/// presses Create there.
/// </summary>
public sealed class ProposalsViewModel : ObservableObject
{
    private readonly ProposalBook _book;
    private readonly Func<IReadOnlyList<WorkstreamCardViewModel>> _cards;
    private readonly Action<WorkstreamProposal> _review;
    private readonly HashSet<string> _announced = new(StringComparer.Ordinal);
    private bool _seeded;

    /// <summary>Creates the view over the proposal book.</summary>
    /// <param name="cards">The cards on the board, read on every update.</param>
    /// <param name="review">Opens the composer for a proposal.</param>
    public ProposalsViewModel(ProposalBook book, Func<IReadOnlyList<WorkstreamCardViewModel>> cards, Action<WorkstreamProposal> review)
    {
        _book = book;
        _cards = cards;
        _review = review;
    }

    /// <summary>The pending proposals, oldest first.</summary>
    public ObservableCollection<ProposalRowViewModel> Pending { get; } = [];

    /// <summary>Shows a proposal's brief in Theseus's Markdown view (Nigel, 2026-11-22). Set by the app.</summary>
    public Action<string>? ShowBrief { get; set; }

    /// <summary>Told, in words, when a proposal arrives that the board has not shown before.</summary>
    public Action<string>? Arrived { get; set; }

    /// <summary>Raised after the rows are rebuilt, so the Chains button can recount.</summary>
    public event EventHandler? Updated;

    /// <summary>Rebuilds the rows from the book. Proposals already waiting at start-up are not announced.</summary>
    public void Update()
    {
        var cards = _cards();
        var pending = _book.Pending;

        Pending.Clear();

        foreach (var proposal in pending)
        {
            var requester = cards.FirstOrDefault(card => string.Equals(card.Key, proposal.RequesterKey, StringComparison.OrdinalIgnoreCase));

            Pending.Add(new ProposalRowViewModel(proposal, requester?.Title ?? proposal.RequesterKey, requester?.ColorHex ?? "#808080",
                _review, Reject, ShowBrief));

            if (_announced.Add(proposal.Id) && _seeded)
            {
                Arrived?.Invoke($"{requester?.Title ?? proposal.RequesterKey} proposes a new workstream, {proposal.Request.Title}. "
                                + "Review it in Chains.");
            }
        }

        _seeded = true;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A proposal by its id, decided or not, or null.</summary>
    public WorkstreamProposal? Find(string proposalId) => _book.Find(proposalId);

    /// <summary>
    /// Every key a proposal has named, as requester or as the card it made, so a new card never takes one (review of 1.1e
    /// PR 3, finding 2): a session controls its descendants by these keys.
    /// </summary>
    public IReadOnlyCollection<string> TakenKeys =>
        [.. _book.All.SelectMany(proposal => new[] { proposal.RequesterKey, proposal.ResultingKey }).OfType<string>()];

    /// <summary>Told when the owner rejects a proposal on the board, so its proposer is told (1.1e AC 27a).</summary>
    public Action<WorkstreamProposal, string?>? Rejected { get; set; }

    /// <summary>Marks a proposal approved once the composer has created its card.</summary>
    public void Approved(string proposalId, string key)
    {
        _book.Approve(proposalId, key);
        Update();
    }

    private string? Reject(string id, string? note)
    {
        var outcome = _book.Reject(id, note);

        if (outcome.Refusal is null)
        {
            if (_book.Find(id) is { } rejected)
            {
                Rejected?.Invoke(rejected, note);
            }

            Update();
        }

        return outcome.Refusal;
    }
}
