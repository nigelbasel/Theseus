using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>One request waiting for a lock, as the board's lock strip shows it.</summary>
public sealed class LockWaiterViewModel : ObservableObject
{
    private int _position;
    private bool _isLast;
    private string _who = string.Empty;
    private string _purpose = string.Empty;
    private string _reason = string.Empty;
    private string _waitingFor = string.Empty;
    private string _colorHex = string.Empty;

    /// <summary>Creates the entry for a ticket, with what Nigel can do to it.</summary>
    public LockWaiterViewModel(string token, Action<LockWaiterViewModel> moveUp, Action<LockWaiterViewModel> moveDown, Action<LockWaiterViewModel> cancel)
    {
        Token = token;
        MoveUpCommand = new RelayCommand(_ => moveUp(this), _ => Position > 1);
        MoveDownCommand = new RelayCommand(_ => moveDown(this), _ => !IsLast);
        CancelCommand = new RelayCommand(_ => cancel(this));
    }

    /// <summary>The ticket, which the board never shows.</summary>
    public string Token { get; }

    /// <summary>Its place in the queue, counting from 1.</summary>
    public int Position
    {
        get => _position;
        set
        {
            if (Set(ref _position, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Whether it is last in the queue, so it cannot move down.</summary>
    public bool IsLast
    {
        get => _isLast;
        set
        {
            if (Set(ref _isLast, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Who asked.</summary>
    public string Who
    {
        get => _who;
        set => Set(ref _who, value);
    }

    /// <summary>What for.</summary>
    public string Purpose
    {
        get => _purpose;
        set => Set(ref _purpose, value);
    }

    /// <summary>Why it matters, as the requester put it, or empty.</summary>
    public string Reason
    {
        get => _reason;
        set => Set(ref _reason, value);
    }

    /// <summary>How long it has waited.</summary>
    public string WaitingFor
    {
        get => _waitingFor;
        set => Set(ref _waitingFor, value);
    }

    /// <summary>The tab colour of the workstream that asked, or empty when no card matches.</summary>
    public string ColorHex
    {
        get => _colorHex;
        set => Set(ref _colorHex, value);
    }

    /// <summary>Moves it one place towards the front.</summary>
    public RelayCommand MoveUpCommand { get; }

    /// <summary>Moves it one place back.</summary>
    public RelayCommand MoveDownCommand { get; }

    /// <summary>Takes it out of the queue; its script is told it was cancelled.</summary>
    public RelayCommand CancelCommand { get; }
}
