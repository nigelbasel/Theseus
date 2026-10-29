using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Proposals;

/// <summary>
/// Every workstream a session has proposed, and the only thing that changes them. A session files and
/// withdraws its own; only the board approves or rejects. The same title from the same workstream
/// while one is pending updates that one rather than making another, because retries, re-sent calls
/// and subagents sharing the session's endpoint all press the same button twice.
///
/// Sessions call in on the MCP server's pool threads while the board decides on the UI thread, so every
/// read and write takes the lock, and <see cref="Changed"/> is raised outside it.
/// </summary>
public sealed class ProposalBook
{
    /// <summary>How many proposals one workstream may have waiting at once.</summary>
    public const int MaximumPending = 3;

    private readonly ProposalStateStore _store;
    private readonly ISystemClock _clock;
    private readonly List<WorkstreamProposal> _proposals;
    private readonly Lock _gate = new();

    /// <summary>Opens the book from its saved state.</summary>
    public ProposalBook(ProposalStateStore store, ISystemClock clock)
    {
        _store = store;
        _clock = clock;
        _proposals = [.. store.Load()];
    }

    /// <summary>Raised after any change is saved, on the thread that made it.</summary>
    public event EventHandler? Changed;

    /// <summary>Every proposal ever made.</summary>
    public IReadOnlyList<WorkstreamProposal> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _proposals];
            }
        }
    }

    /// <summary>The proposals waiting for Nigel, oldest first.</summary>
    public IReadOnlyList<WorkstreamProposal> Pending
    {
        get
        {
            lock (_gate)
            {
                return [.. _proposals.Where(proposal => proposal.IsPending).OrderBy(proposal => proposal.CreatedUtc)];
            }
        }
    }

    /// <summary>One workstream's proposals, newest first.</summary>
    public IReadOnlyList<WorkstreamProposal> From(string requesterKey)
    {
        lock (_gate)
        {
            return [.. _proposals.Where(proposal => Same(proposal.RequesterKey, requesterKey)).OrderByDescending(proposal => proposal.CreatedUtc)];
        }
    }

    /// <summary>The proposal with this id, decided or not.</summary>
    public WorkstreamProposal? Find(string id)
    {
        lock (_gate)
        {
            return _proposals.FirstOrDefault(proposal => Same(proposal.Id, id));
        }
    }

    /// <summary>Files a proposal that has passed <see cref="ProposalCheck"/>, or updates the pending one it repeats.</summary>
    public ProposalOutcome Propose(string requesterKey, string sessionId, ProposalRequest request, ProposalCheckResult check)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(check);

        ProposalOutcome outcome;

        lock (_gate)
        {
            outcome = TryPropose(requesterKey, sessionId, request, check);
        }

        if (outcome.Refusal is { } refusal)
        {
            TheseusLog.Information("Proposal {Title} from {From} (session {SessionId}) refused: {Refusal}", request.Title, requesterKey,
                sessionId, refusal);
        }
        else
        {
            TheseusLog.Information("Proposal {Id} {What} from {From} (session {SessionId}): {Title}, brief {Brief}, {Warnings} warning(s)",
                outcome.Proposal!.Id, outcome.IsRepeat ? "updated" : "filed", requesterKey, sessionId, request.Title, request.Brief,
                outcome.Proposal.Warnings.Count);
            OnChanged();
        }

        return outcome;
    }

    /// <summary>Takes back a pending proposal. Only the workstream that made it may.</summary>
    public ProposalOutcome Withdraw(string requesterKey, string sessionId, string id) =>
        Decide(id, "withdrawn",
            proposal => Same(proposal.RequesterKey, requesterKey)
                ? null
                : "That proposal was made by another workstream, so this one cannot withdraw it.",
            ProposalStatus.Withdrawn, $"withdrawn by session {sessionId}", null);

    /// <summary>Marks a proposal approved, once its card exists. Only the board calls this.</summary>
    public ProposalOutcome Approve(string id, string resultingKey) =>
        Decide(id, "approved", _ => null, ProposalStatus.Approved, null, resultingKey);

    /// <summary>Turns a proposal down, with Nigel's note if he gave one. Only the board calls this.</summary>
    public ProposalOutcome Reject(string id, string? note) =>
        Decide(id, "rejected", _ => null, ProposalStatus.Rejected, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), null);

    private ProposalOutcome TryPropose(string requesterKey, string sessionId, ProposalRequest request, ProposalCheckResult check)
    {
        if (check.Refusal is { } refusal)
        {
            return ProposalOutcome.Refused(refusal);
        }

        var pending = _proposals.Where(proposal => proposal.IsPending && Same(proposal.RequesterKey, requesterKey)).ToList();
        var title = Normalised(request.Title);

        if (pending.FirstOrDefault(proposal => Normalised(proposal.Request.Title) == title) is { } repeat)
        {
            var updated = repeat with { SessionId = sessionId, Request = request, Warnings = check.Warnings };

            _proposals[_proposals.IndexOf(repeat)] = updated;
            Save();

            return new ProposalOutcome(updated, null, IsRepeat: true);
        }

        if (pending.Count >= MaximumPending)
        {
            return ProposalOutcome.Refused($"{MaximumPending} proposals from this workstream are already waiting for the user. "
                                           + "Withdraw one, or wait for the user to decide.");
        }

        var proposal = new WorkstreamProposal(NewId(), ProposalStatus.Pending, requesterKey, sessionId, request, check.Warnings,
            _clock.UtcNow);

        _proposals.Add(proposal);
        Save();

        return new ProposalOutcome(proposal, null);
    }

    private ProposalOutcome Decide(string id, string what, Func<WorkstreamProposal, string?> refusal, ProposalStatus status,
        string? note, string? resultingKey)
    {
        ProposalOutcome outcome;

        lock (_gate)
        {
            var index = _proposals.FindIndex(proposal => Same(proposal.Id, id));

            if (index < 0)
            {
                outcome = ProposalOutcome.Refused($"There is no proposal {id}.");
            }
            else if (!_proposals[index].IsPending)
            {
                outcome = ProposalOutcome.Refused($"Proposal {id} is already {_proposals[index].Status.ToString().ToLowerInvariant()}.");
            }
            else if (refusal(_proposals[index]) is { } refused)
            {
                outcome = ProposalOutcome.Refused(refused);
            }
            else
            {
                _proposals[index] = _proposals[index] with
                {
                    Status = status,
                    DecidedUtc = _clock.UtcNow,
                    DecisionNote = note,
                    ResultingKey = resultingKey,
                };
                Save();
                outcome = new ProposalOutcome(_proposals[index], null);
            }
        }

        if (outcome.Refusal is { } reason)
        {
            TheseusLog.Information("Proposal {Id} not {What}: {Refusal}", id, what, reason);
        }
        else
        {
            TheseusLog.Information("Proposal {Id} {What}{Detail}", id, what, resultingKey is null ? string.Empty : $" as {resultingKey}");
            OnChanged();
        }

        return outcome;
    }

    private static string Normalised(string title) =>
        string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string NewId() => $"pr-{Guid.NewGuid():N}"[..11];

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void Save() => _store.Save(_proposals);
}
