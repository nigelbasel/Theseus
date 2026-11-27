namespace Theseus.App.ViewModels;

/// <summary>One hold on a resource row: who, how much of it, for how long, and Nigel's release, which asks first.</summary>
public sealed class LockHolderViewModel : ObservableObject
{
    private string _text = string.Empty;
    private string _amount = string.Empty;
    private string _heldFor = string.Empty;
    private string _colorHex = string.Empty;
    private string _idle = string.Empty;
    private bool _isLong;
    private bool _isConfirmingRelease;

    public LockHolderViewModel(string token, Action<string> release)
    {
        Token = token;
        AskReleaseCommand = new RelayCommand(_ => IsConfirmingRelease = true);
        ConfirmReleaseCommand = new RelayCommand(_ =>
        {
            IsConfirmingRelease = false;
            release(Token);
        });
        KeepCommand = new RelayCommand(_ => IsConfirmingRelease = false);
    }

    public string Token { get; }

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    /// <summary>How much of the resource it holds, for a resource with several slots; empty for one.</summary>
    public string Amount
    {
        get => _amount;
        set => Set(ref _amount, value);
    }

    public string HeldFor
    {
        get => _heldFor;
        set => Set(ref _heldFor, value);
    }

    public string ColorHex
    {
        get => _colorHex;
        set => Set(ref _colorHex, value);
    }

    public bool IsLong
    {
        get => _isLong;
        set => Set(ref _isLong, value);
    }

    /// <summary>For a session hold whose session has been idle half an hour or has gone: how long, for Nigel to release.</summary>
    public string Idle
    {
        get => _idle;
        set
        {
            if (Set(ref _idle, value))
            {
                Raise(nameof(IsAmber));
            }
        }
    }

    public bool IsAmber => Idle.Length > 0;

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

    public bool CanAskRelease => !IsConfirmingRelease;

    public RelayCommand AskReleaseCommand { get; }

    public RelayCommand ConfirmReleaseCommand { get; }

    public RelayCommand KeepCommand { get; }
}
