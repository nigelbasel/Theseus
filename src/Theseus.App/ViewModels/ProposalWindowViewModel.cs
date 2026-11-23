using Theseus.Core.Proposals;

namespace Theseus.App.ViewModels;

/// <summary>
/// The one window for a session's proposal (sessions-run-the-project items 0.1, 0.2), whichever route opened it: what is
/// proposed and why, its brief drawn, with Approve, Reject… and Cancel. Approving is a yes or no on what the session
/// wrote, so nothing in it is editable; the session is responsible for the brief.
/// </summary>
public sealed class ProposalWindowViewModel : ObservableObject
{
    private readonly Func<WorkstreamProposal, string?> _whyNotApprove;
    private readonly Func<WorkstreamProposal, string?> _approve;
    private readonly Func<WorkstreamProposal, string?, string?> _reject;
    private string _notice = string.Empty;
    private string _note = string.Empty;
    private bool _isRejecting;

    /// <summary>Creates the window's model for a pending proposal.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="requesterTitle">The proposing card's title.</param>
    /// <param name="brief">The brief's Markdown, or null when its file cannot be read.</param>
    /// <param name="whyNotApprove">Why it cannot be approved now, or null when it can.</param>
    /// <param name="approve">Makes its card; why not, or null when it was made.</param>
    /// <param name="reject">Rejects it with the note; why not, or null when it was rejected.</param>
    public ProposalWindowViewModel(WorkstreamProposal proposal, string requesterTitle, string? brief,
        Func<WorkstreamProposal, string?> whyNotApprove, Func<WorkstreamProposal, string?> approve,
        Func<WorkstreamProposal, string?, string?> reject)
    {
        Proposal = proposal ?? throw new ArgumentNullException(nameof(proposal));
        RequesterTitle = requesterTitle;
        Brief = brief;
        _whyNotApprove = whyNotApprove;
        _approve = approve;
        _reject = reject;

        ApproveCommand = new RelayCommand(_ => Approve(), _ => !_isRejecting && Block.Length == 0);
        AskRejectCommand = new RelayCommand(_ => IsRejecting = true, _ => !_isRejecting);
        RejectCommand = new RelayCommand(_ => Reject(), _ => _isRejecting);
        CancelCommand = new RelayCommand(_ => Cancel());
    }

    /// <summary>The proposal shown.</summary>
    public WorkstreamProposal Proposal { get; }

    /// <summary>The proposing card's title.</summary>
    public string RequesterTitle { get; }

    /// <summary>The brief's Markdown, or null when it cannot be read.</summary>
    public string? Brief { get; }

    /// <summary>The window's title.</summary>
    public string Title => $"Proposal: {Proposal.Request.Title}";

    /// <summary>"Auth research proposes Cache spike".</summary>
    public string Heading => $"{RequesterTitle} proposes {Proposal.Request.Title}";

    /// <summary>The objective it serves, or that it is ad hoc.</summary>
    public string Objective => Proposal.Request.Objective is { } objective ? $"Under objective {objective}" : "Ad hoc: under no objective";

    /// <summary>Its records folder, branch and repos, as the session named them.</summary>
    public string Where => string.Join(" · ", new[]
    {
        $"Records: {Proposal.Request.Slug ?? "not named"}",
        $"Branch: {Proposal.Request.Branch ?? "none"}",
        $"Repos: {(Proposal.Request.Repos.Count == 0 ? "read-only" : string.Join(", ", Proposal.Request.Repos))}",
    });

    /// <summary>Whether it reports back, and why it is wanted.</summary>
    public string Detail => string.Join(" ", new[]
    {
        Proposal.Request.ReportBack ? $"Reports back to {RequesterTitle} when done." : $"Carries {RequesterTitle}'s work on.",
        Proposal.Request.Reason is { } reason ? $"“{reason}”" : string.Empty,
    }.Where(part => part.Length > 0));

    /// <summary>What it must produce.</summary>
    public string DoneCriteria => $"Done when: {Proposal.Request.DoneCriteria}";

    /// <summary>Its Jira keys, or empty.</summary>
    public string JiraKeys => Proposal.Request.JiraKeys.Count == 0 ? string.Empty : $"Jira: {string.Join(", ", Proposal.Request.JiraKeys)}";

    /// <summary>The warnings worked out when it arrived, one per line.</summary>
    public string Warnings => string.Join("\n", Proposal.Warnings.Select(warning => $"⚠ {warning}"));

    /// <summary>Where the brief is, and what was sent.</summary>
    public string Evidence =>
        $"From session {Proposal.SessionId}, {Proposal.CreatedUtc.ToLocalTime():ddd d MMM HH:mm}. Brief {Proposal.Request.Brief}";

    /// <summary>Why it cannot be approved now, or empty.</summary>
    public string Block => _whyNotApprove(Proposal) is { } why ? $"Cannot be approved yet: {why}" : string.Empty;

    /// <summary>Why the last press did nothing, or empty.</summary>
    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    /// <summary>Whether the note box for a rejection is showing.</summary>
    public bool IsRejecting
    {
        get => _isRejecting;
        private set => Set(ref _isRejecting, value);
    }

    /// <summary>The owner's note on a rejection, which the proposer reads.</summary>
    public string Note
    {
        get => _note;
        set => Set(ref _note, value ?? string.Empty);
    }

    /// <summary>Makes the card; the window closes when it is made.</summary>
    public RelayCommand ApproveCommand { get; }

    /// <summary>Shows the note box.</summary>
    public RelayCommand AskRejectCommand { get; }

    /// <summary>Rejects it with the note; the window closes when it is rejected.</summary>
    public RelayCommand RejectCommand { get; }

    /// <summary>Hides the note box, or closes the window with nothing decided.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>Raised when the window should close: a decision was made, or Cancel was pressed outside a rejection.</summary>
    public event EventHandler? Closing;

    private void Approve()
    {
        if (_approve(Proposal) is { } refusal)
        {
            Notice = refusal;
            Raise(nameof(Block));

            return;
        }

        Closing?.Invoke(this, EventArgs.Empty);
    }

    private void Reject()
    {
        if (_reject(Proposal, Note.Trim() is { Length: > 0 } note ? note : null) is { } refusal)
        {
            Notice = refusal;

            return;
        }

        Closing?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        if (_isRejecting)
        {
            IsRejecting = false;
            Note = string.Empty;

            return;
        }

        Closing?.Invoke(this, EventArgs.Empty);
    }
}
