using System.Collections.ObjectModel;
using Theseus.Core.Locks;

namespace Theseus.App.ViewModels;

/// <summary>
/// One lock on the board: who holds it and for how long, and the queue behind it. Updated in place on
/// every refresh, so an unchanged lock raises nothing for UI Automation to report.
/// </summary>
public sealed class LockRowViewModel : ObservableObject
{
    private readonly Action<LockWaiterViewModel> _moveUp;
    private readonly Action<LockWaiterViewModel> _moveDown;
    private readonly Action<LockWaiterViewModel> _cancel;
    private bool _isHeld;
    private bool _isLong;
    private bool _isConfirmingRelease;
    private string _holder = string.Empty;
    private string _heldFor = string.Empty;

    /// <summary>Creates the row for one lock.</summary>
    public LockRowViewModel(
        string name,
        Action<string> release,
        Action<LockWaiterViewModel> moveUp,
        Action<LockWaiterViewModel> moveDown,
        Action<LockWaiterViewModel> cancel)
    {
        Name = name;
        Guards = LockNames.Describe(name);
        _moveUp = moveUp;
        _moveDown = moveDown;
        _cancel = cancel;
        AskReleaseCommand = new RelayCommand(_ => IsConfirmingRelease = true);
        ConfirmReleaseCommand = new RelayCommand(_ =>
        {
            IsConfirmingRelease = false;
            release(Name);
        });
        KeepCommand = new RelayCommand(_ => IsConfirmingRelease = false);
    }

    /// <summary>The lock's name.</summary>
    public string Name { get; }

    /// <summary>What it guards.</summary>
    public string Guards { get; }

    /// <summary>The requests waiting, in order.</summary>
    public ObservableCollection<LockWaiterViewModel> Waiting { get; } = [];

    /// <summary>Whether someone holds it.</summary>
    public bool IsHeld
    {
        get => _isHeld;
        private set
        {
            if (Set(ref _isHeld, value))
            {
                Raise(nameof(IsActive));
                Raise(nameof(CanAskRelease));
            }
        }
    }

    /// <summary>Whether it has been held longer than <see cref="LocksViewModel.LongHold"/>.</summary>
    public bool IsLong
    {
        get => _isLong;
        private set => Set(ref _isLong, value);
    }

    /// <summary>Whether it is held or anyone is waiting, which is when the board shows it.</summary>
    public bool IsActive => IsHeld || Waiting.Count > 0;

    /// <summary>Who holds it and what for.</summary>
    public string Holder
    {
        get => _holder;
        private set => Set(ref _holder, value);
    }

    /// <summary>How long it has been held.</summary>
    public string HeldFor
    {
        get => _heldFor;
        private set => Set(ref _heldFor, value);
    }

    /// <summary>Whether Release has been pressed and is waiting for a second press.</summary>
    public bool IsConfirmingRelease
    {
        get => _isConfirmingRelease;
        set
        {
            if (Set(ref _isConfirmingRelease, value))
            {
                Raise(nameof(CanAskRelease));
            }
        }
    }

    /// <summary>Whether the first Release button shows: held, and not already asking.</summary>
    public bool CanAskRelease => IsHeld && !IsConfirmingRelease;

    /// <summary>
    /// Asks before releasing, because releasing does not stop the holder's run: another may then start
    /// beside it, which is the overlap the lock exists to prevent.
    /// </summary>
    public RelayCommand AskReleaseCommand { get; }

    /// <summary>Releases the lock from its holder.</summary>
    public RelayCommand ConfirmReleaseCommand { get; }

    /// <summary>Leaves the holder alone.</summary>
    public RelayCommand KeepCommand { get; }

    /// <summary>Brings the row into line with the lock book.</summary>
    public void Update(IReadOnlyList<LockTicket> tickets, DateTimeOffset now)
    {
        var holder = tickets.FirstOrDefault(ticket => ticket.IsHolder && ticket.Lock == Name);
        var waiting = tickets.Where(ticket => !ticket.IsHolder && ticket.Lock == Name).ToList();

        IsHeld = holder is not null;
        Holder = holder is null ? string.Empty : $"{holder.Who} — {holder.Purpose}";
        HeldFor = holder?.GrantedUtc is { } granted ? LocksViewModel.Say(now - granted) : string.Empty;
        IsLong = holder?.GrantedUtc is { } since && now - since > LocksViewModel.LongHold;

        if (!IsHeld)
        {
            IsConfirmingRelease = false;
        }

        for (var index = Waiting.Count - 1; index >= 0; index--)
        {
            if (!waiting.Any(ticket => ticket.Token == Waiting[index].Token))
            {
                Waiting.RemoveAt(index);
            }
        }

        for (var index = 0; index < waiting.Count; index++)
        {
            var ticket = waiting[index];
            var existing = Waiting.FirstOrDefault(entry => entry.Token == ticket.Token);

            if (existing is null)
            {
                existing = new LockWaiterViewModel(ticket.Token, _moveUp, _moveDown, _cancel);
                Waiting.Insert(index, existing);
            }
            else if (Waiting.IndexOf(existing) != index)
            {
                Waiting.Move(Waiting.IndexOf(existing), index);
            }

            existing.Position = index + 1;
            existing.IsLast = index == waiting.Count - 1;
            existing.Who = ticket.Who;
            existing.Purpose = ticket.Purpose;
            existing.Reason = ticket.Reason ?? string.Empty;
            existing.WaitingFor = LocksViewModel.Say(now - ticket.RequestedUtc);
        }

        Raise(nameof(IsActive));
    }

    /// <summary>The row's part of the status bar, or empty when the lock is free with nobody waiting.</summary>
    public string Summary =>
        !IsActive
            ? string.Empty
            : $"{Name}: {(IsHeld ? $"held {HeldFor}" : "free")}{(Waiting.Count > 0 ? $", {Waiting.Count} waiting" : string.Empty)}";
}
