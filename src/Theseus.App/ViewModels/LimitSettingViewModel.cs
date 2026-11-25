using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>One headless limit on Options' Headless page: its value, what it does, and Change….</summary>
public sealed class LimitSettingViewModel : ObservableObject
{
    private readonly HeadlessLimitSetting _setting;
    private readonly ISettingsStore _store;
    private string _value = string.Empty;

    /// <summary>Creates the row.</summary>
    /// <param name="setting">The limit.</param>
    /// <param name="store">Where settings are kept.</param>
    /// <param name="ask">Asks for a new value: title, label and current value in; the typed value, or null when cancelled, out.</param>
    public LimitSettingViewModel(HeadlessLimitSetting setting, ISettingsStore store, Func<string, string, string, string?> ask)
    {
        _setting = setting;
        _store = store;
        ChangeCommand = new RelayCommand(_ => Change(ask));
        Reload();
    }

    /// <summary>Its name.</summary>
    public string Label => _setting.Label;

    /// <summary>What it does, with its default.</summary>
    public string Explanation => _setting.Explanation;

    /// <summary>Its value now.</summary>
    public string Value
    {
        get => _value;
        private set => Set(ref _value, value);
    }

    /// <summary>The Change… button's accessible name.</summary>
    public string ChangeName => $"Change {_setting.Label.ToLowerInvariant()}";

    /// <summary>Asks for a new value and keeps it if it is a valid one.</summary>
    public RelayCommand ChangeCommand { get; }

    /// <summary>Reads the value again from the settings.</summary>
    public void Reload() => Value = _setting.Show(_store.Read());

    private void Change(Func<string, string, string, string?> ask)
    {
        var current = _store.Read();

        while (ask(_setting.Label, _setting.Explanation, _setting.Show(current)) is { } typed)
        {
            if (_setting.Apply(current, typed) is { } changed)
            {
                _store.Write(changed);
                Reload();

                return;
            }
        }
    }
}
