using System.Diagnostics;
using System.IO;
using Theseus.Core.Proposals;

namespace Theseus.App.ViewModels;

/// <summary>
/// One workstream a session has proposed, under Proposed in the Chains tab. Review opens the new
/// workstream composer filled in from the proposal, and creating the card there is the approval;
/// closing the composer leaves it pending. Reject asks for an optional note, which the proposer reads
/// back through <c>proposal_status</c>.
/// </summary>
public sealed class ProposalRowViewModel : ObservableObject
{
    private readonly Func<string, string?, string?> _reject;
    private string _note = string.Empty;
    private string _refusal = string.Empty;
    private bool _isRejecting;

    /// <summary>Creates the row for one pending proposal.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="requesterTitle">The proposing card's title, or its key when it is not on the board.</param>
    /// <param name="colorHex">The proposing card's colour, as the row's left border.</param>
    /// <param name="review">Opens the composer for this proposal.</param>
    /// <param name="reject">Rejects it with a note, returning why not if it was refused.</param>
    public ProposalRowViewModel(WorkstreamProposal proposal, string requesterTitle, string colorHex,
        Action<WorkstreamProposal> review, Func<string, string?, string?> reject)
    {
        Proposal = proposal;
        RequesterTitle = requesterTitle;
        ColorHex = colorHex;
        _reject = reject;

        ReviewCommand = new RelayCommand(_ => review(proposal));
        OpenBriefCommand = new RelayCommand(_ => Open(proposal.Request.Brief), _ => File.Exists(proposal.Request.Brief));
        AskRejectCommand = new RelayCommand(_ => IsRejecting = true);
        CancelRejectCommand = new RelayCommand(_ =>
        {
            IsRejecting = false;
            Note = string.Empty;
        });
        RejectCommand = new RelayCommand(_ => Refusal = _reject(proposal.Id, Note) ?? string.Empty);
    }

    /// <summary>The proposal shown.</summary>
    public WorkstreamProposal Proposal { get; }

    /// <summary>Who proposed it.</summary>
    public string RequesterTitle { get; }

    /// <summary>The proposing card's colour.</summary>
    public string ColorHex { get; }

    /// <summary>"Auth research proposes Cache spike".</summary>
    public string Heading => $"{RequesterTitle} proposes {Proposal.Request.Title}";

    /// <summary>Whether it reports back, and why it is wanted.</summary>
    public string Detail => string.Join(" ", new[]
    {
        Proposal.Request.ReportBack ? "Reports back when done." : "Carries the work on.",
        Proposal.Request.Reason is { } reason ? $"“{reason}”" : string.Empty,
    }.Where(part => part.Length > 0));

    /// <summary>What it must produce.</summary>
    public string DoneCriteria => $"Done when: {Proposal.Request.DoneCriteria}";

    /// <summary>The warnings worked out when it arrived, one per line.</summary>
    public string Warnings => string.Join("\n", Proposal.Warnings.Select(warning => $"⚠ {warning}"));

    /// <summary>The session that sent it, which subagents share, so the row says it was an agent.</summary>
    public string Evidence =>
        $"Agent-originated: session {Proposal.SessionId}, {Proposal.CreatedUtc.ToLocalTime():ddd HH:mm}. Brief {Proposal.Request.Brief}";

    /// <summary>Opens the composer filled in from the proposal.</summary>
    public RelayCommand ReviewCommand { get; }

    /// <summary>Opens the brief.</summary>
    public RelayCommand OpenBriefCommand { get; }

    /// <summary>Shows the note box.</summary>
    public RelayCommand AskRejectCommand { get; }

    /// <summary>Hides the note box.</summary>
    public RelayCommand CancelRejectCommand { get; }

    /// <summary>Rejects it with the note.</summary>
    public RelayCommand RejectCommand { get; }

    /// <summary>Whether the note box is showing.</summary>
    public bool IsRejecting
    {
        get => _isRejecting;
        set => Set(ref _isRejecting, value);
    }

    /// <summary>Nigel's note on the rejection, which the proposer reads.</summary>
    public string Note
    {
        get => _note;
        set => Set(ref _note, value);
    }

    /// <summary>Why the last press did nothing.</summary>
    public string Refusal
    {
        get => _refusal;
        private set => Set(ref _refusal, value);
    }

    private static void Open(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
