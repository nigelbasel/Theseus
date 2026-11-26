using Theseus.Core.Diagnostics;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Options, Advanced: the card whose sessions may rule on resources the lock table does not know. The lock client,
/// its guard and the command classes are installed with the tests-lock guard.
/// </summary>
public sealed class LockOptionsViewModel : ObservableObject
{
    public const string NoArbiter = "None";

    private readonly ISettingsStore _store;
    private readonly Func<IReadOnlyList<(string Key, string Title)>> _cards;
    private IReadOnlyList<string> _arbiterChoices = [NoArbiter];
    private string _arbiter = NoArbiter;
    private bool _loading;

    public LockOptionsViewModel(ISettingsStore store, Func<IReadOnlyList<(string Key, string Title)>> cards)
    {
        _store = store;
        _cards = cards;
    }

    /// <summary>None, then every card on the board by title.</summary>
    public IReadOnlyList<string> ArbiterChoices
    {
        get => _arbiterChoices;
        private set => Set(ref _arbiterChoices, value);
    }

    public string Arbiter
    {
        get => _arbiter;
        set
        {
            if (!Set(ref _arbiter, value) || _loading)
            {
                return;
            }

            string? key = value == NoArbiter ? null : _cards().FirstOrDefault(card => card.Title == value).Key;
            _store.Write(_store.Read() with { LockArbiterCard = key });
            TheseusLog.Information("Lock arbiter set to {Card}", key ?? "none");
        }
    }

    public void Load()
    {
        _loading = true;

        try
        {
            IReadOnlyList<(string Key, string Title)> cards = _cards();
            ArbiterChoices = [NoArbiter, .. cards.Select(card => card.Title)];
            string? key = _store.Read().LockArbiterCard;
            Arbiter = cards.FirstOrDefault(card => string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase)).Title ?? NoArbiter;
        }
        finally
        {
            _loading = false;
        }
    }
}
