using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Stopping;

/// <summary>
/// Whether a Theseus stop hook is registered, and the configuration that would register one.
///
/// The settings file belongs to the user. It is read and never written: a tool that edits the
/// configuration governing what runs inside every session on the machine, on its own initiative, is
/// a tool nobody can reason about. Theseus reports what it finds and hands over the text.
/// </summary>
public static class HookInstallation
{
    /// <summary>The events a stop hook is registered on.</summary>
    public static IReadOnlyList<string> Events { get; } = ["Stop", "UserPromptSubmit"];

    /// <summary>Where Claude Code keeps the settings a hook is registered in.</summary>
    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    /// <summary>
    /// Whether any hook command in the settings runs the Theseus hook. Matched on the executable's
    /// name rather than its full path, so moving the installation does not read as uninstalled.
    /// </summary>
    public static bool IsInstalled(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);

            return document.RootElement.TryGetProperty("hooks", out var hooks)
                   && Mentions(hooks);
        }
        catch (JsonException exception)
        {
            TheseusLog.Debug("The settings could not be read to look for a hook: {Reason}", exception.Message);

            return false;
        }
    }

    /// <summary>
    /// The configuration to add, ready to be shown to the user.
    ///
    /// The path is quoted inside the command, which matters more than it looks: the command is run
    /// through a shell, and an unquoted <c>C:\Program Files\...</c> is two words to a shell. It fails
    /// as "C:Program: command not found", from inside somebody else's session, where nothing connects
    /// it to Theseus.
    /// </summary>
    public static string Snippet(string hookExecutablePath)
    {
        var quoted = JsonSerializer.Serialize($"\"{hookExecutablePath}\"");

        var events = Events.Select(name =>
            $$"""
                  "{{name}}": [
                    {
                      "hooks": [
                        { "type": "command", "command": {{quoted}} }
                      ]
                    }
                  ]
              """);

        return $$"""
                 {
                   "hooks": {
                 {{string.Join($",{Environment.NewLine}", events)}}
                   }
                 }
                 """;
    }

    private static bool Mentions(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() is { } text
                                && text.Contains("Theseus.Hook", StringComparison.OrdinalIgnoreCase),
        JsonValueKind.Object => element.EnumerateObject().Any(property => Mentions(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(Mentions),
        _ => false,
    };
}
