using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Theseus.Core.Guards;

/// <summary>
/// A guard in a Claude Code configuration folder, installed at the owner's click (verifier-runs brief step 3, Nigel
/// 2026-11-22; every shipped guard, guards-ship-with-theseus, Nigel 2026-11-26): Theseus, never a session, copies its own
/// guard and the files it needs to where the hook runs it, keeping each old copy as <c>.previous</c>, checks it took, and
/// can put the old copies back. A missing hook entry is added only after its exact change has been shown.
/// </summary>
public sealed class GuardInstallation
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
    private readonly Regex _guardInCommand;

    /// <summary>A guard in a configuration folder.</summary>
    /// <param name="definition">Which guard.</param>
    /// <param name="configFolder">The Claude Code configuration folder: <c>CLAUDE_CONFIG_DIR</c>, else <c>~\.claude</c>.</param>
    /// <param name="home">The user's home, which <c>~</c> and <c>$HOME</c> name in a hook command; null for this user's.</param>
    public GuardInstallation(GuardDefinition definition, string configFolder, string? home = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);

        Definition = definition;
        _config = Path.GetFullPath(configFolder);
        _home = Path.GetFullPath(home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        SettingsPath = Path.Combine(_config, "settings.json");

        // A path to the guard in a hook command: quoted (it may hold spaces) or bare, ending in a folder then the guard's
        // own name, so my-trunk-guard.js or a .bak copy is not the guard.
        var name = Regex.Escape(definition.Guard.FileName);
        _guardInCommand = new Regex(
            $"""(?:"(?<path>[^"]*[\\/]{name})"|'(?<path>[^']*[\\/]{name})'|(?<path>[^\s"'<>|]*[\\/]{name}))(?![\w.])""",
            RegexOptions.IgnoreCase);
    }

    /// <summary>Which guard this is.</summary>
    public GuardDefinition Definition { get; }

    /// <summary>Claude Code's settings file, where the hook is registered.</summary>
    public string SettingsPath { get; }

    /// <summary>The configuration folder this user's Claude Code reads.</summary>
    public static string UserConfigFolder() =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    /// <summary>The installed guard as it is now.</summary>
    public GuardState State()
    {
        var commands = HookCommands(ReadSettings());
        var guard = GuardPath(commands);
        var installed = File.Exists(guard);
        var files = Placed(guard);

        return new GuardState(Definition.Name, guard, installed, installed && files.All(file => IsCurrent(file.Path, file.File)),
            installed ? File.GetLastWriteTime(guard) : null,
            commands.Any(hook => Covers(hook.Matcher) && Named(hook.Command).Any()),
            files.Any(file => File.Exists(file.Path + PreviousSuffix)));
    }

    /// <summary>Copies this Theseus's guard and the files it needs into place, keeping the old copies, and checks it took.</summary>
    public GuardOutcome Install()
    {
        var guard = State().GuardPath;

        // A guard run from elsewhere, such as a repo's tools folder, is a file someone else keeps (review of the install, S3).
        if (!Path.GetFullPath(guard).StartsWith(_config + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return new GuardOutcome(false,
                $"The hook runs the {Definition.Name} from {guard}, outside Claude Code's folder, so Theseus will not overwrite it. Point the hook at "
                + $"{Path.Combine(_config, "bin", Definition.Guard.FileName)} and install again.");
        }

        var stale = Placed(guard).Where(file => !IsCurrent(file.Path, file.File)).ToList();

        if (stale.Count == 0)
        {
            var current = Prove();

            return current with { Message = $"The {Definition.Name} at {guard} is this Theseus's already. {current.Message}" };
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(guard)!);

            foreach (var (path, file) in stale)
            {
                if (File.Exists(path))
                {
                    File.Copy(path, path + PreviousSuffix, overwrite: true);
                }

                WriteWhole(path, file.Text);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GuardOutcome(false, $"Could not write {guard}: {exception.Message}");
        }

        var proof = Prove();
        var unhooked = State().HookIsRegistered ? string.Empty : " Claude Code's settings do not run it yet: add the hook entry.";

        return proof with { Message = $"Installed the {Definition.Name} at {guard}. {proof.Message}{unhooked}" };
    }

    /// <summary>Puts back the copies the last install replaced.</summary>
    public GuardOutcome Undo()
    {
        var guard = State().GuardPath;
        var kept = Placed(guard).Where(file => File.Exists(file.Path + PreviousSuffix)).ToList();

        if (kept.Count == 0)
        {
            return new GuardOutcome(false, $"There is no earlier copy of the {Definition.Name} to put back.");
        }

        try
        {
            foreach (var (path, _) in kept)
            {
                File.Move(path + PreviousSuffix, path, overwrite: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GuardOutcome(false, $"Could not put back the earlier {Definition.Name}: {exception.Message}");
        }

        return new GuardOutcome(true, $"Put back the {Definition.Name} that was at {guard} before the last install.");
    }

    /// <summary>Checks the installed guard on a fixture folder.</summary>
    public GuardOutcome Prove() => Definition.Prove(State().GuardPath);

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
    public GuardOutcome AddHookEntry()
    {
        var text = ReadSettings();

        if (State().HookIsRegistered)
        {
            return new GuardOutcome(true, "The hook is registered already.");
        }

        if (Editable(text) is not { } settings)
        {
            return new GuardOutcome(false,
                $"{SettingsPath} does not read as settings with a hooks.PreToolUse list, so nothing was changed. Add the entry by hand.");
        }

        var hooks = settings["hooks"] as JsonObject ?? new JsonObject();
        settings["hooks"] = hooks;
        var preToolUse = hooks["PreToolUse"] as JsonArray ?? new JsonArray();
        hooks["PreToolUse"] = preToolUse;
        preToolUse.Add(HookEntry(State().GuardPath));

        try
        {
            Directory.CreateDirectory(_config);

            if (text is not null)
            {
                File.Copy(SettingsPath, SettingsPath + PreviousSuffix, overwrite: true);
            }

            WriteWhole(SettingsPath, settings.ToJsonString(Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GuardOutcome(false, $"Could not write {SettingsPath}: {exception.Message}");
        }

        return new GuardOutcome(true,
            $"Registered the {Definition.Name} in {SettingsPath}; the old file is kept beside it. Sessions started from now on run it.");
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

    private static bool IsCurrent(string path, GuardFile file) =>
        File.Exists(path) && GuardFile.VersionOf(File.ReadAllText(path)) == file.Version;

    // The guard where the hook runs it, and each file it needs beside it.
    private IReadOnlyList<(string Path, GuardFile File)> Placed(string guard) =>
    [
        (guard, Definition.Guard),
        .. Definition.Companions.Select(file => (Path.Combine(Path.GetDirectoryName(guard)!, file.FileName), file)),
    ];

    private JsonObject HookEntry(string guard) => new()
    {
        ["matcher"] = Definition.Matcher,
        ["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command",
            ["command"] = $"node \"{guard.Replace('\\', '/')}\" 2>/dev/null",
            ["timeout"] = 10,
        }),
    };

    // Whether an entry's matcher runs it before every tool the guard must see: every tool for a guard of every tool, else
    // each of its tools (review of the install, S6: a holdout guard for Bash alone guards nothing else).
    private bool Covers(string? matcher)
    {
        if (matcher is null or "" or "*" or ".*")
        {
            return true;
        }

        if (Definition.Tools.Count == 0)
        {
            return false;
        }

        try
        {
            var pattern = new Regex($"^(?:{matcher})$", RegexOptions.None, TimeSpan.FromSeconds(1));

            return Definition.Tools.All(pattern.IsMatch);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private string? ReadSettings() => File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;

    // Where the hook runs the guard: the first path a hook command names, as the shell would expand it, else bin\<guard>
    // in the configuration folder.
    private string GuardPath(IReadOnlyList<HookCommand> commands) =>
        commands.SelectMany(hook => Named(hook.Command)).FirstOrDefault()
        ?? Path.Combine(_config, "bin", Definition.Guard.FileName);

    private IEnumerable<string> Named(string command) =>
        _guardInCommand.Matches(command)
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

        return GitBashDrive.Replace(expanded, match => $"{match.Groups["drive"].Value.ToUpperInvariant()}:/");
    }

    private static readonly Regex GitBashDrive = new("^/(?:mnt/)?(?<drive>[a-zA-Z])/", RegexOptions.Compiled);

    // Each PreToolUse hook command, with its entry's matcher. Never throws: Theseus asks at start.
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
                        .Select(hook => new HookCommand(hook["command"]!.GetValue<string>(), MatcherOf(entry))))]
                : [];
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return [];
        }
    }

    // A matcher that is not a string is read as matching nothing in particular rather than everything.
    private static string? MatcherOf(JsonObject entry) =>
        entry["matcher"] switch
        {
            null => null,
            JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
            _ => "\0",
        };
}
