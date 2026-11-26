using Theseus.Core.Diagnostics;
using Theseus.Core.Guards;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Options, Guards, Auto mode's environment: what the owner enters once (his source-control hosts, internal domains and
/// production words), kept in his Theseus settings, and the environment built from them, written to Claude Code's settings
/// only at his click after the exact change is shown (guards-ship-with-theseus brief step 3, Nigel 2026-11-26).
/// </summary>
public sealed class AutoModeViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly AutoModeEnvironment _environment;
    private string _result = string.Empty;

    /// <summary>Creates the section over the Theseus settings and a Claude Code configuration folder's auto mode.</summary>
    public AutoModeViewModel(ISettingsStore store, AutoModeEnvironment environment)
    {
        _store = store;
        _environment = environment;
        WriteCommand = new RelayCommand(_ => WriteEnvironment());
    }

    /// <summary>Asks the owner to agree to a change, shown in full; true to make it. Set by the app.</summary>
    public Func<string, bool>? ConfirmChange { get; set; }

    /// <summary>The entries as saved.</summary>
    public AutoModeEntries Entries
    {
        get
        {
            var settings = _store.Read();

            return new AutoModeEntries(settings.AutoModeSourceControlHosts, settings.AutoModeInternalDomains, settings.AutoModeProductionPatterns);
        }
    }

    /// <summary>The source-control hosts, as a row shows them.</summary>
    public string SourceControlHostsValue => Shown(Entries.SourceControlHosts);

    /// <summary>The internal domains, as a row shows them.</summary>
    public string InternalDomainsValue => Shown(Entries.InternalDomains);

    /// <summary>The production words, as a row shows them.</summary>
    public string ProductionPatternsValue => Shown(Entries.ProductionPatterns);

    /// <summary>Whether Claude Code's settings hold the environment the entries build.</summary>
    public string Line =>
        _environment.Matches(Entries) ? "Written; it matches your entries"
        : _environment.Current() is null ? "Not written"
        : "Differs from your entries";

    /// <summary>Where it is written.</summary>
    public string SettingsPath => _environment.SettingsPath;

    /// <summary>What the last write came to, or empty.</summary>
    public string Result
    {
        get => _result;
        private set => Set(ref _result, value);
    }

    /// <summary>Writes the environment to Claude Code's settings, after showing the change.</summary>
    public RelayCommand WriteCommand { get; }

    /// <summary>Saves the source-control hosts from a comma-separated list.</summary>
    public void SetSourceControlHosts(string text) => Save(settings => settings with { AutoModeSourceControlHosts = Split(text) });

    /// <summary>Saves the internal domains from a comma-separated list.</summary>
    public void SetInternalDomains(string text) => Save(settings => settings with { AutoModeInternalDomains = Split(text) });

    /// <summary>Saves the production words from a comma-separated list.</summary>
    public void SetProductionPatterns(string text) => Save(settings => settings with { AutoModeProductionPatterns = Split(text) });

    /// <summary>Reads everything again, as Options opens.</summary>
    public void Load()
    {
        Result = string.Empty;
        RaiseAll();
    }

    /// <summary>A list as the prompt to change it starts: comma-separated.</summary>
    public static string Joined(IReadOnlyList<string> items) => string.Join(", ", items);

    /// <summary>A comma- or semicolon-separated list, each item trimmed, empty ones dropped.</summary>
    public static IReadOnlyList<string> Split(string text) =>
        [.. (text ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static string Shown(IReadOnlyList<string> items) => items.Count == 0 ? "None" : Joined(items);

    private void Save(Func<TheseusSettings, TheseusSettings> change)
    {
        _store.Write(change(_store.Read()));
        Result = string.Empty;
        RaiseAll();
    }

    private void WriteEnvironment()
    {
        var entries = Entries;

        if (_environment.Change(entries) is not { } change)
        {
            Result = _environment.Matches(entries)
                ? "Claude Code's settings already hold this environment."
                : $"{_environment.SettingsPath} does not read as settings with an autoMode object, so nothing was changed.";
            RaiseAll();

            return;
        }

        if (ConfirmChange?.Invoke(change) != true)
        {
            Result = "Nothing was changed.";

            return;
        }

        var outcome = _environment.Write(entries);
        TheseusLog.Information("Auto mode's environment: {Result}", outcome.Message);
        RaiseAll();
        Result = outcome.Message;
    }

    private void RaiseAll()
    {
        Raise(nameof(SourceControlHostsValue));
        Raise(nameof(InternalDomainsValue));
        Raise(nameof(ProductionPatternsValue));
        Raise(nameof(Line));
    }
}
