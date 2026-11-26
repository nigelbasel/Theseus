using System.Collections.ObjectModel;
using Theseus.Core.Locks;

namespace Theseus.App.ViewModels;

/// <summary>
/// One resource on the locks panel: its capacity, every hold on it with how much it takes, and the queue of requests
/// that need it. A request for several resources shows on each of them; a hold taken as <c>tests</c> shows on all three
/// of its resources.
/// </summary>
public sealed class LockRowViewModel : ObservableObject
{
    private readonly Action<string> _release;
    private readonly Action<LockWaiterViewModel> _moveUp;
    private readonly Action<LockWaiterViewModel> _moveDown;
    private readonly Action<LockWaiterViewModel> _cancel;
    private bool _isHeld;
    private bool _isLong;
    private bool _isConfirmingRelease;
    private string _holder = string.Empty;
    private string _heldFor = string.Empty;
    private string _holderColorHex = string.Empty;
    private string _capacity = string.Empty;
    private string _guards;

    public LockRowViewModel(
        string name,
        Action<string> release,
        Action<LockWaiterViewModel> moveUp,
        Action<LockWaiterViewModel> moveDown,
        Action<LockWaiterViewModel> cancel)
    {
        Name = name;
        _guards = LockNames.Describe(name);
        _release = release;
        _moveUp = moveUp;
        _moveDown = moveDown;
        _cancel = cancel;
        AskReleaseCommand = new RelayCommand(_ => IsConfirmingRelease = true);
        ConfirmReleaseCommand = new RelayCommand(_ =>
        {
            IsConfirmingRelease = false;

            if (Holders.FirstOrDefault() is { } first)
            {
                release(first.Token);
            }
        });
        KeepCommand = new RelayCommand(_ => IsConfirmingRelease = false);
    }

    public string Name { get; }

    public string Guards
    {
        get => _guards;
        private set => Set(ref _guards, value);
    }

    /// <summary>How many holds of one slot it takes, and how many are taken, for a resource with several.</summary>
    public string Capacity
    {
        get => _capacity;
        private set => Set(ref _capacity, value);
    }

    public ObservableCollection<LockHolderViewModel> Holders { get; } = [];

    public ObservableCollection<LockWaiterViewModel> Waiting { get; } = [];

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

    public bool IsLong
    {
        get => _isLong;
        private set => Set(ref _isLong, value);
    }

    public bool HasAmberHold => Holders.Any(holder => holder.IsAmber);

    public bool IsActive => IsHeld || Waiting.Count > 0;

    public string Holder
    {
        get => _holder;
        private set => Set(ref _holder, value);
    }

    public string HeldFor
    {
        get => _heldFor;
        private set => Set(ref _heldFor, value);
    }

    public string HolderColorHex
    {
        get => _holderColorHex;
        private set => Set(ref _holderColorHex, value);
    }

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

    public bool CanAskRelease => IsHeld && !IsConfirmingRelease;

    public RelayCommand AskReleaseCommand { get; }

    public RelayCommand ConfirmReleaseCommand { get; }

    public RelayCommand KeepCommand { get; }

    public void Update(
        IReadOnlyList<LockTicket> tickets,
        DateTimeOffset now,
        Func<LockTicket, string?>? colourOf = null,
        ResourceTable? table = null,
        Func<LockTicket, string?>? idleOf = null)
    {
        List<LockTicket> mine = [.. tickets.Where(Uses)];
        List<LockTicket> holders = [.. mine.Where(ticket => ticket.IsHolder)];
        List<LockTicket> waiting = [.. mine.Where(ticket => !ticket.IsHolder)];
        LockTicket? first = holders.FirstOrDefault();
        int capacity = table?.Capacity(Name) ?? 1;

        if (table?.Rule(Name) is { } rule && LockNames.Find(Name) is null)
        {
            Guards = rule.Why;
        }

        Capacity = capacity == 1 ? string.Empty : $"{holders.Sum(ticket => Amount(ticket, table))} of {capacity} slots taken";
        IsHeld = first is not null;
        Holder = first is null ? string.Empty : $"{first.Who} — {first.Purpose}";
        HeldFor = first?.GrantedUtc is { } granted ? LocksViewModel.Say(now - granted) : string.Empty;
        IsLong = holders.Any(ticket => ticket.GrantedUtc is { } since && now - since > LocksViewModel.LongHold);
        HolderColorHex = first is null ? string.Empty : colourOf?.Invoke(first) ?? string.Empty;

        if (!IsHeld)
        {
            IsConfirmingRelease = false;
        }

        UpdateHolders(holders, now, colourOf, table, idleOf, capacity);
        UpdateWaiting(waiting, now, colourOf);

        Raise(nameof(IsActive));
        Raise(nameof(HasAmberHold));
    }

    public string Summary =>
        !IsActive
            ? string.Empty
            : $"{Name}: {(IsHeld ? $"held {HeldFor}" : "free")}{(Waiting.Count > 0 ? $", {Waiting.Count} waiting" : string.Empty)}";

    private bool Uses(LockTicket ticket) =>
        string.Equals(ticket.Lock, Name, StringComparison.Ordinal) || ticket.Needs(Name);

    private int Amount(LockTicket ticket, ResourceTable? table) =>
        ticket.Claims.FirstOrDefault(claim => claim.Resource == Name) is { } claim
            ? table?.Amount(claim) ?? 1
            : 1;

    private void UpdateHolders(List<LockTicket> holders, DateTimeOffset now, Func<LockTicket, string?>? colourOf,
        ResourceTable? table, Func<LockTicket, string?>? idleOf, int capacity)
    {
        for (int index = Holders.Count - 1; index >= 0; index--)
        {
            if (!holders.Any(ticket => ticket.Token == Holders[index].Token))
            {
                Holders.RemoveAt(index);
            }
        }

        for (int index = 0; index < holders.Count; index++)
        {
            LockTicket ticket = holders[index];
            LockHolderViewModel? existing = Holders.FirstOrDefault(entry => entry.Token == ticket.Token);

            if (existing is null)
            {
                existing = new LockHolderViewModel(ticket.Token, _release);
                Holders.Insert(index, existing);
            }
            else if (Holders.IndexOf(existing) != index)
            {
                Holders.Move(Holders.IndexOf(existing), index);
            }

            existing.Text = $"{ticket.Who} — {ticket.Purpose}";
            existing.Amount = capacity == 1 ? string.Empty : $"{Amount(ticket, table)} of {capacity}";
            existing.HeldFor = ticket.GrantedUtc is { } granted ? LocksViewModel.Say(now - granted) : string.Empty;
            existing.IsLong = ticket.GrantedUtc is { } since && now - since > LocksViewModel.LongHold;
            existing.ColorHex = colourOf?.Invoke(ticket) ?? string.Empty;
            existing.Idle = idleOf?.Invoke(ticket) ?? string.Empty;
        }
    }

    private void UpdateWaiting(List<LockTicket> waiting, DateTimeOffset now, Func<LockTicket, string?>? colourOf)
    {
        for (int index = Waiting.Count - 1; index >= 0; index--)
        {
            if (!waiting.Any(ticket => ticket.Token == Waiting[index].Token))
            {
                Waiting.RemoveAt(index);
            }
        }

        for (int index = 0; index < waiting.Count; index++)
        {
            LockTicket ticket = waiting[index];
            LockWaiterViewModel? existing = Waiting.FirstOrDefault(entry => entry.Token == ticket.Token);

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
            existing.Previous = index > 0 ? waiting[index - 1].Token : null;
            existing.Next = index < waiting.Count - 1 ? waiting[index + 1].Token : null;
            existing.Who = ticket.Who;
            existing.Purpose = ticket.Purpose;
            existing.Reason = ticket.Reason ?? string.Empty;
            existing.WaitingFor = LocksViewModel.Say(now - ticket.RequestedUtc);
            existing.ColorHex = colourOf?.Invoke(ticket) ?? string.Empty;
        }
    }
}
