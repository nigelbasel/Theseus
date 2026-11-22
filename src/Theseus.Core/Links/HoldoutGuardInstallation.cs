using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Theseus.Core.Links;

/// <summary>
/// The holdout guard in a Claude Code configuration folder, installed at the owner's click (verifier-runs brief step 3,
/// Nigel 2026-11-22): Theseus, never a session, copies its own guard to where the hook runs it, keeping the old copy as
/// <c>holdout-guard.js.previous</c>, checks it took, and can put the old copy back. A missing hook entry is added only
/// after its exact change has been shown.
/// </summary>
public sealed partial class HoldoutGuardInstallation
{
    private const string PreviousSuffix = ".previous";

    // Claude Code's settings are a file people read: written escaped, every quote, & and accent became \uXXXX.
    private static readonly JsonSerializerOptions Readable = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _config;
    private readonly string _home;

    /// <summary>The guard in a configuration folder.</summary>
    /// <param name="configFolder">The Claude Code configuration folder: <c>CLAUDE_CONFIG_DIR</c>, else <c>~\.claude</c>.</param>
    /// <param name="home">The user's home, which <c>~</c> and <c>$HOME</c> name in a hook command; null for this user's.</param>
    public HoldoutGuardInstallation(string configFolder, string? home = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);

        _config = Path.GetFullPath(configFolder);
        _home = Path.GetFullPath(home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        SettingsPath = Path.Combine(_config, "settings.json");
    }

    /// <summary>Claude Code's settings file, where the hook is registered.</summary>
    public string SettingsPath { get; }

    /// <summary>The configuration folder this user's Claude Code reads.</summary>
    public static string UserConfigFolder() =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    /// <summary>The installed guard as it is now.</summary>
    public HoldoutGuardState State()
    {
        var commands = HookCommands(ReadSettings());
        var guard = GuardPath(commands);
        var installed = File.Exists(guard);

        return new HoldoutGuardState(guard, installed, installed && IsBundled(guard), installed ? File.GetLastWriteTime(guard) : null,
            commands.Any(hook => hook.Everything && Named(hook.Command).Any()), File.Exists(guard + PreviousSuffix));
    }

    /// <summary>Copies this Theseus's guard into place, keeping the old copy, and checks it took.</summary>
    public HoldoutGuardOutcome Install()
    {
        var guard = State().GuardPath;

        // A guard run from elsewhere, such as a repo's tools folder, is a file someone else keeps (review of the install, S3).
        if (!Path.GetFullPath(guard).StartsWith(_config + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return new HoldoutGuardOutcome(false,
                $"The hook runs the guard from {guard}, outside Claude Code's folder, so Theseus will not overwrite it. Point the hook at "
                + $"{Path.Combine(_config, "bin", HoldoutGuardUpgrade.FileName)} and install again.");
        }

        if (File.Exists(guard) && IsBundled(guard))
        {
            var current = Prove();

            return current with { Message = $"The holdout guard at {guard} is this Theseus's already. {current.Message}" };
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(guard)!);

            if (File.Exists(guard))
            {
                File.Copy(guard, guard + PreviousSuffix, overwrite: true);
            }

            WriteWhole(guard, HoldoutGuardBundle.Text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HoldoutGuardOutcome(false, $"Could not write {guard}: {exception.Message}");
        }

        var proof = Prove();
        var unhooked = State().HookIsRegistered ? string.Empty : " Claude Code's settings do not run it yet: add the hook entry.";

        return proof with { Message = $"Installed the holdout guard at {guard}. {proof.Message}{unhooked}" };
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
    /// null when the hook is registered already, or the settings cannot be changed safely.
    /// </summary>
    public string? HookEntryChange()
    {
        var state = State();

        return state.HookIsRegistered || Editable(ReadSettings()) is null
            ? null
            : $"Add to hooks.PreToolUse in {SettingsPath}:{Environment.NewLine}{HookEntry(state.GuardPath).ToJsonString(Readable)}";
    }

    /// <summary>Registers the guard in Claude Code's settings, keeping the old file as <c>settings.json.previous</c>.</summary>
    public HoldoutGuardOutcome AddHookEntry()
    {
        var text = ReadSettings();

        if (State().HookIsRegistered)
        {
            return new HoldoutGuardOutcome(true, "The hook is registered already.");
        }

        if (Editable(text) is not { } settings)
        {
            return new HoldoutGuardOutcome(false,
                $"{SettingsPath} does not read as settings with a hooks.PreToolUse list, so nothing was changed. Add the entry by hand.");
        }

        var hooks = settings["hooks"] as JsonObject ?? new JsonObject();
        settings["hooks"] = hooks;
        var preToolUse = hooks["PreToolUse"] as JsonArray ?? new JsonArray();
        hooks["PreToolUse"] = preToolUse;
        preToolUse.Add(HookEntry(State().GuardPath));

        try
        {
            if (text is not null)
            {
                File.Copy(SettingsPath, SettingsPath + PreviousSuffix, overwrite: true);
            }

            WriteWhole(SettingsPath, settings.ToJsonString(Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HoldoutGuardOutcome(false, $"Could not write {SettingsPath}: {exception.Message}");
        }

        return new HoldoutGuardOutcome(true,
            $"Registered the holdout guard in {SettingsPath}; the old file is kept beside it. Sessions started from now on run it.");
    }

    // The settings as an object the entry can be added to without losing anything, or null: unreadable, a duplicate key,
    // or hooks or hooks.PreToolUse of another shape, which a rewrite would replace (review of the install, M1, M3).
    private static JsonObject? Editable(string? text)
    {
        if (text is null)
        {
            return new JsonObject();
        }

        try
        {
            using var strict = JsonDocument.Parse(text, new JsonDocumentOptions { AllowDuplicateProperties = false });

            if (JsonNode.Parse(text) is not JsonObject settings)
            {
                return null;
            }

            var hooks = settings["hooks"];

            return (hooks is null || (hooks is JsonObject && hooks["PreToolUse"] is null or JsonArray)) ? settings : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteWhole(string path, string text)
    {
        var staged = path + ".installing";
        File.WriteAllText(staged, text);
        File.Move(staged, path, overwrite: true);
    }

    private static bool IsBundled(string guard) =>
        HoldoutGuardBundle.VersionOf(File.ReadAllText(guard)) == HoldoutGuardBundle.Version;

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

    // Where the hook runs the guard: the first path a hook command names, as the shell would expand it, else
    // bin\holdout-guard.js in the configuration folder.
    private string GuardPath(IReadOnlyList<HookCommand> commands) =>
        commands.SelectMany(hook => Named(hook.Command)).FirstOrDefault()
        ?? Path.Combine(_config, "bin", HoldoutGuardUpgrade.FileName);

    private IEnumerable<string> Named(string command) =>
        GuardInCommand().Matches(command)
            .Select(match => Expand(match.Groups["path"].Value))
            .Where(Path.IsPathFullyQualified)
            .Select(Path.GetFullPath);

    private string Expand(string written)
    {
        var expanded = written
            .Replace("${HOME}", _home, StringComparison.Ordinal)
            .Replace("$HOME", _home, StringComparison.Ordinal)
            .Replace("${CLAUDE_CONFIG_DIR}", _config, StringComparison.Ordinal)
            .Replace("$CLAUDE_CONFIG_DIR", _config, StringComparison.Ordinal)
            .Replace("%USERPROFILE%", _home, StringComparison.OrdinalIgnoreCase)
            .Replace("$USERPROFILE", _home, StringComparison.Ordinal);

        if (expanded.StartsWith('~'))
        {
            expanded = _home + expanded[1..];
        }

        return GitBashDrive().Replace(expanded, match => $"{match.Groups["drive"].Value.ToUpperInvariant()}:/");
    }

    // Each PreToolUse hook command, and whether it runs before every tool. Never throws: Theseus asks at start.
    private static IReadOnlyList<HookCommand> HookCommands(string? settings)
    {
        if (settings is null)
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(settings)?["hooks"]?["PreToolUse"] is JsonArray entries
                ? [.. entries.OfType<JsonObject>()
                    .SelectMany(entry => (entry["hooks"] as JsonArray ?? [])
                        .OfType<JsonObject>()
                        .Where(hook => hook["command"]?.GetValueKind() == JsonValueKind.String)
                        .Select(hook => new HookCommand(hook["command"]!.GetValue<string>(), RunsBeforeEveryTool(entry))))]
                : [];
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return [];
        }
    }

    private static bool RunsBeforeEveryTool(JsonObject entry) =>
        entry["matcher"] is null
        || (entry["matcher"]!.GetValueKind() == JsonValueKind.String && entry["matcher"]!.GetValue<string>() is "" or "*" or ".*");

    // A path to the guard in a hook command: quoted (it may hold spaces) or bare, ending in the guard's own name.
    [GeneratedRegex("""(?:"(?<path>[^"]*holdout-guard\.js)"|'(?<path>[^']*holdout-guard\.js)'|(?<path>[^\s"'<>|]*holdout-guard\.js))(?![\w.])""",
        RegexOptions.IgnoreCase)]
    private static partial Regex GuardInCommand();

    [GeneratedRegex("^/(?:mnt/)?(?<drive>[a-zA-Z])/")]
    private static partial Regex GitBashDrive();
}
