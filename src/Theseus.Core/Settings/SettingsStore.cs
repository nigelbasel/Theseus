using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Settings;

/// <summary>
/// A settings file that can always be read. A corrupted or partial file falls back to the defaults
/// rather than stopping the application, because there is no board at all until this succeeds.
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private readonly string _path;

    /// <summary>Creates a store over a <c>settings.json</c> file.</summary>
    public SettingsStore(string path) => _path = path;

    /// <inheritdoc />
    public TheseusSettings Read()
    {
        var text = AtomicFile.TryReadAllText(_path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return TheseusSettings.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<TheseusSettings>(text, TheseusJson.Readable);

            return settings is null ? TheseusSettings.Default : Complete(settings);
        }
        catch (JsonException exception)
        {
            TheseusLog.Error(exception, "Settings at {Path} could not be read, so the defaults are in force", _path);

            return TheseusSettings.Default;
        }
    }

    /// <inheritdoc />
    public void Write(TheseusSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(settings, TheseusJson.Readable));
    }

    private static TheseusSettings Complete(TheseusSettings settings) => settings with
    {
        PlanDirectory = Fallback(settings.PlanDirectory, TheseusSettings.Default.PlanDirectory),
        TerminalWindowName = Fallback(settings.TerminalWindowName, TheseusSettings.Default.TerminalWindowName),
        TerminalExecutablePath = settings.TerminalExecutablePath ?? string.Empty,
        ClaudeExecutablePath = settings.ClaudeExecutablePath ?? string.Empty,
        JiraBaseUrl = settings.JiraBaseUrl ?? string.Empty,
        SessionPreamble = settings.SessionPreamble ?? string.Empty,
        ContinuePrompt = settings.ContinuePrompt ?? string.Empty,
        StopPrompt = settings.StopPrompt ?? string.Empty,
        IdentityPrompt = settings.IdentityPrompt ?? string.Empty,
        Lanes = Domain.BoardLanes.Sanitise(settings.Lanes),
    };

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
