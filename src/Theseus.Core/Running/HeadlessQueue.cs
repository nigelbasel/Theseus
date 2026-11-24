namespace Theseus.Core.Running;

/// <summary>
/// The machine-wide cap on headless runs at once, and the queue behind it: runs beyond the cap wait, and start in the
/// order they were asked for as runs end (objective 2.2). A card is never in it twice.
/// </summary>
public sealed class HeadlessQueue
{
    private readonly Lock _gate = new();
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _waiting = [];

    /// <summary>Creates the queue with its cap.</summary>
    public HeadlessQueue(int cap) => Cap = cap;

    /// <summary>How many headless runs may go at once; below one is read as one.</summary>
    public int Cap { get; set; }

    /// <summary>How many are going.</summary>
    public int Running
    {
        get
        {
            lock (_gate)
            {
                return _running.Count;
            }
        }
    }

    /// <summary>How many are waiting for a slot.</summary>
    public int Queued
    {
        get
        {
            lock (_gate)
            {
                return _waiting.Count;
            }
        }
    }

    /// <summary>Whether a card is going or waiting.</summary>
    public bool Holds(string workstream)
    {
        lock (_gate)
        {
            return _running.Contains(workstream) || _waiting.Contains(workstream, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>A card's place in the queue, 1 for next, or null when it is not waiting.</summary>
    public int? PositionOf(string workstream)
    {
        lock (_gate)
        {
            var index = _waiting.FindIndex(key => string.Equals(key, workstream, StringComparison.OrdinalIgnoreCase));

            return index < 0 ? null : index + 1;
        }
    }

    /// <summary>Takes a slot for a card at once if one is free and nobody is waiting; otherwise queues it.</summary>
    /// <returns>Null when it may start now; otherwise its place in the queue.</returns>
    public int? Enter(string workstream)
    {
        lock (_gate)
        {
            if (_running.Contains(workstream))
            {
                return null;
            }

            if (_waiting.Count == 0 && _running.Count < Math.Max(1, Cap))
            {
                _running.Add(workstream);

                return null;
            }

            if (!_waiting.Contains(workstream, StringComparer.OrdinalIgnoreCase))
            {
                _waiting.Add(workstream);
            }

            return PositionOf(workstream);
        }
    }

    /// <summary>Frees a card's slot, or takes it out of the queue.</summary>
    public void Leave(string workstream)
    {
        lock (_gate)
        {
            _running.Remove(workstream);
            _waiting.RemoveAll(key => string.Equals(key, workstream, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>The next waiting card, given a slot, if one is free.</summary>
    public string? TakeNext()
    {
        lock (_gate)
        {
            if (_waiting.Count == 0 || _running.Count >= Math.Max(1, Cap))
            {
                return null;
            }

            var next = _waiting[0];
            _waiting.RemoveAt(0);
            _running.Add(next);

            return next;
        }
    }
}
