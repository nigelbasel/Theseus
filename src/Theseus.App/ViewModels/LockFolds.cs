using System.ComponentModel;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Every fold of the locks panel, by key (<c>lock:&lt;name&gt;</c>, <c>queue:&lt;name&gt;</c>, <c>section:&lt;name&gt;</c>), so the
/// panel reads as a list of lock names to unfold for details and actions (Nigel, 2026-11-26). A fold changed by hand is
/// remembered in settings; Expand all and Collapse all replace what was remembered with the one choice, as the Chains
/// panel's do.
/// </summary>
public sealed class LockFolds
{
    private readonly Dictionary<string, FoldState> _states = new(StringComparer.Ordinal);
    private Dictionary<string, bool> _remembered = new(StringComparer.Ordinal);
    private ISettingsStore? _settings;
    private bool _bulk;

    public LockFolds()
    {
        ExpandAllCommand = new RelayCommand(_ => SetAll(true));
        CollapseAllCommand = new RelayCommand(_ => SetAll(false));
    }

    /// <summary>Unfolds every lock, queue and section.</summary>
    public RelayCommand ExpandAllCommand { get; }

    /// <summary>Folds every lock, queue and section.</summary>
    public RelayCommand CollapseAllCommand { get; }

    /// <summary>Where the folds are kept between runs; read when set, and applied to folds already made.</summary>
    public ISettingsStore? Settings
    {
        get => _settings;
        set
        {
            _settings = value;

            if (value?.Read() is not { } read)
            {
                return;
            }

            _remembered = new Dictionary<string, bool>(read.LocksFolds, StringComparer.Ordinal);
            _bulk = true;

            foreach (var state in _states.Values)
            {
                if (_remembered.TryGetValue(state.Key, out var open))
                {
                    state.IsExpanded = open;
                }
            }

            _bulk = false;
        }
    }

    /// <summary>The fold for a key: as remembered, or <paramref name="open"/> when it never was.</summary>
    public FoldState For(string key, bool open)
    {
        if (_states.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var state = new FoldState(key, _remembered.TryGetValue(key, out var remembered) ? remembered : open);
        state.PropertyChanged += Changed;
        _states[key] = state;

        return state;
    }

    private void SetAll(bool open)
    {
        _bulk = true;
        _remembered.Clear();

        foreach (var state in _states.Values)
        {
            state.IsExpanded = open;
            _remembered[state.Key] = open;
        }

        _bulk = false;
        Save();
    }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_bulk || e.PropertyName != nameof(FoldState.IsExpanded) || sender is not FoldState state)
        {
            return;
        }

        _remembered[state.Key] = state.IsExpanded;
        Save();
    }

    private void Save()
    {
        if (_settings is { } settings)
        {
            settings.Write(settings.Read() with { LocksFolds = new Dictionary<string, bool>(_remembered, StringComparer.Ordinal) });
        }
    }
}
