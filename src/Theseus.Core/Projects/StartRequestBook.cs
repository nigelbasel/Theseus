using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Every request from a project's session to start a card the owner paused (sessions-run-the-project items 12 to 15), and
/// the only thing that changes them. One request waits per card: asking again updates it. Sessions ask on the MCP
/// server's threads while the owner answers on the UI thread, so every read and write takes the lock.
/// </summary>
public sealed class StartRequestBook
{
    private readonly StartRequestStore _store;
    private readonly ISystemClock _clock;
    private readonly List<StartRequest> _requests;
    private readonly Lock _gate = new();

    /// <summary>Opens the book from its saved state.</summary>
    public StartRequestBook(StartRequestStore store, ISystemClock clock)
    {
        _store = store;
        _clock = clock;
        _requests = [.. store.Load()];
    }

    /// <summary>Raised after any change is saved, on the thread that made it.</summary>
    public event EventHandler? Changed;

    /// <summary>The requests waiting for the owner, oldest first.</summary>
    public IReadOnlyList<StartRequest> Pending
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests.Where(request => request.IsPending).OrderBy(request => request.CreatedUtc)];
            }
        }
    }

    /// <summary>The request with this id, decided or not.</summary>
    public StartRequest? Find(string id)
    {
        lock (_gate)
        {
            return _requests.FirstOrDefault(request => Same(request.Id, id));
        }
    }

    /// <summary>
    /// Files a request to start a card, or updates the reason and instruction of the one already waiting for it, and
    /// returns it with whether it repeated one. A repeat keeps the session that asked first, which is the one told the
    /// answer (review of PR 2, finding 5).
    /// </summary>
    public (StartRequest Request, bool IsRepeat) Ask(string cardKey, string requesterKey, string? instruction, string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requesterKey);

        (StartRequest, bool) asked;

        lock (_gate)
        {
            var index = _requests.FindIndex(request => request.IsPending && Same(request.CardKey, cardKey));

            if (index >= 0)
            {
                _requests[index] = _requests[index] with
                {
                    Instruction = instruction ?? _requests[index].Instruction,
                    Reason = reason ?? _requests[index].Reason,
                };
                asked = (_requests[index], true);
            }
            else
            {
                var request = new StartRequest($"sr-{Guid.NewGuid():N}"[..11], cardKey, requesterKey, instruction, reason, _clock.UtcNow);
                _requests.Add(request);
                asked = (request, false);
            }

            _store.Save(_requests);
        }

        TheseusLog.Information("Start request {Id} for {Card} from {From}", asked.Item1.Id, cardKey, requesterKey);
        Changed?.Invoke(this, EventArgs.Empty);

        return asked;
    }

    /// <summary>Records the owner's answer, or a lapse; the request as decided, or null when it was not pending.</summary>
    public StartRequest? Decide(string id, StartRequestStatus status, string? note)
    {
        if (status == StartRequestStatus.Pending)
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A decision leaves pending.");
        }

        StartRequest? decided = null;

        lock (_gate)
        {
            var index = _requests.FindIndex(request => request.IsPending && Same(request.Id, id));

            if (index >= 0)
            {
                _requests[index] = _requests[index] with
                {
                    Status = status,
                    DecidedUtc = _clock.UtcNow,
                    Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                };
                decided = _requests[index];
                _store.Save(_requests);
            }
        }

        if (decided is not null)
        {
            TheseusLog.Information("Start request {Id} for {Card}: {Status}", id, decided.CardKey, status);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return decided;
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
