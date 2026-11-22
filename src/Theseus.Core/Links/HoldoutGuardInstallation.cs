using System.Text.Json;
using System.Text.Json.Nodes;

namespace Theseus.Core.Links;

/// <summary>
/// The holdout guard in a Claude Code configuration folder, installed at the owner's click (verifier-runs brief step 3,
/// Nigel 2026-11-22): Theseus, never a session, copies its own guard to where the hook runs it, keeping the old copy as
/// <c>holdout-guard.js.previous</c>, checks it took, and can put the old copy back. A missing hook entry is added only
/// after its exact change has been shown.
/// </summary>
/// <param name="configFolder">The Claude Code configuration folder: <c>CLAUDE_CONFIG_DIR</c>, else <c>~\.claude</c>.</param>
public sealed class HoldoutGuardInstallation(string configFolder)
{
    private const string PreviousSuffix = ".previous";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Claude Code's settings file, where the hook is registered.</summary>
    public string SettingsPath { get; } = Path.Combine(configFolder, "settings.json");

    /// <summary>The configuration folder this user's Claude Code reads.</summary>
    public static string UserConfigFolder() =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    /// <summary>The installed guard as it is now.</summary>
    public HoldoutGuardState State()
    {
        var settings = ReadSettings();
        var guard = GuardPath(settings);
        var installed = File.Exists(guard);
        var upToDate = installed && HoldoutGuardBundle.VersionOf(File.ReadAllText(guard)) == HoldoutGuardBundle.Version;

        return new HoldoutGuardState(guard, installed, upToDate, installed ? File.GetLastWriteTime(guard) : null,
            HookIsRegistered(settings), File.Exists(guard + PreviousSuffix));
    }

    /// <summary>Copies this Theseus's guard into place, keeping the old copy, and checks it took.</summary>
    public HoldoutGuardOutcome Install()
    {
        var guard = State().GuardPath;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(guard)!);

            if (File.Exists(guard))
            {
                File.Copy(guard, guard + PreviousSuffix, overwrite: true);
            }

            File.WriteAllText(guard, HoldoutGuardBundle.Text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HoldoutGuardOutcome(false, $"Could not write {guard}: {exception.Message}");
        }

        var proof = Prove();

        return proof with { Message = $"Installed the holdout guard at {guard}. {proof.Message}" };
    }

    /// <summary>Puts back the copy the last install replaced.</summary>
    public HoldoutGuardOutcome Undo()
    {
        var guard = State().GuardPath;
        var previous = guard + PreviousSuffix;

        if (!File.Exists(previous))
        {
            return new HoldoutGuardOutcome(false, "There is no earlier copy of the holdout guard to put back.");
        }

        try
        {
            File.Move(previous, guard, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HoldoutGuardOutcome(false, $"Could not put back {previous}: {exception.Message}");
        }

        return new HoldoutGuardOutcome(true, $"Put back the holdout guard that was at {guard} before the last install.");
    }

    /// <summary>Checks the installed guard on a fixture folder.</summary>
    public HoldoutGuardOutcome Prove() => HoldoutGuardProof.Run(State().GuardPath);

    /// <summary>
    /// The exact change that registers the guard in Claude Code's settings, for the owner to read before it is made;
    /// null when the hook is registered already.
    /// </summary>
    public string? HookEntryChange()
    {
        var settings = ReadSettings();

        return HookIsRegistered(settings)
            ? null
            : $"Add to hooks.PreToolUse in {SettingsPath}:{Environment.NewLine}{HookEntry(GuardPath(settings)).ToJsonString(Indented)}";
    }

    /// <summary>Registers the guard in Claude Code's settings, keeping the old file as <c>settings.json.previous</c>.</summary>
    public HoldoutGuardOutcome AddHookEntry()
    {
        var text = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;
        JsonObject settings;

        try
        {
            settings = text is null ? new JsonObject() : JsonNode.Parse(text) as JsonObject
                ?? throw new JsonException("not an object");
        }
        catch (JsonException exception)
        {
            return new HoldoutGuardOutcome(false, $"{SettingsPath} does not read as settings ({exception.Message}), so nothing was changed.");
        }

        if (HookIsRegistered(text))
        {
            return new HoldoutGuardOutcome(true, "The hook is registered already.");
        }

        var hooks = settings["hooks"] as JsonObject ?? new JsonObject();
        settings["hooks"] = hooks;
        var preToolUse = hooks["PreToolUse"] as JsonArray ?? new JsonArray();
        hooks["PreToolUse"] = preToolUse;
        preToolUse.Add(HookEntry(GuardPath(text)));

        try
        {
            if (text is not null)
            {
                File.Copy(SettingsPath, SettingsPath + PreviousSuffix, overwrite: true);
            }

            File.WriteAllText(SettingsPath, settings.ToJsonString(Indented));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HoldoutGuardOutcome(false, $"Could not write {SettingsPath}: {exception.Message}");
        }

        return new HoldoutGuardOutcome(true,
            $"Registered the holdout guard in {SettingsPath}; the old file is kept beside it. Sessions started from now on run it.");
    }

    private static JsonObject HookEntry(string guard) => new()
    {
        ["matcher"] = "*",
        ["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command",
            ["command"] = $"node \"{guard.Replace('\\', '/')}\" 2>/dev/null",
            ["timeout"] = 10,
        }),
    };

    private string? ReadSettings() => File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;

    // Where the hook runs the guard: the first full path a hook command names, else bin\holdout-guard.js. The commands
    // are read as JSON, since a settings file may escape a quote as ", which hides the path from a text search.
    private string GuardPath(string? settings) =>
        HoldoutGuardUpgrade.Candidates(string.Join(Environment.NewLine, HookCommands(settings)), configFolder)
            .Where(candidate => Path.IsPathFullyQualified(candidate))
            .Select(Path.GetFullPath)
            .First();

    private static bool HookIsRegistered(string? settings) =>
        HookCommands(settings).Any(command => command.Contains(HoldoutGuardUpgrade.FileName, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> HookCommands(string? settings)
    {
        if (settings is null)
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(settings)?["hooks"]?["PreToolUse"] is JsonArray entries
                ? [.. entries.OfType<JsonObject>()
                    .SelectMany(entry => entry["hooks"] as JsonArray ?? [])
                    .OfType<JsonObject>()
                    .Select(hook => hook["command"])
                    .Where(command => command?.GetValueKind() == JsonValueKind.String)
                    .Select(command => command!.GetValue<string>())]
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
