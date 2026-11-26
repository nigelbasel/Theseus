using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Theseus.Core.Links;
using Theseus.Core.Stopping;

namespace Theseus.Core.Setup;

/// <summary>
/// The stop hook registered in Claude Code's settings from the first-run setup, only after the owner has read the exact entry
/// and agreed (first-run-setup brief step 1, Nigel 2026-11-25), as Options › Holdouts adds the guard's entry. Theseus still
/// never writes those settings on its own initiative: the owner's click is the only way in, and the old file is kept beside
/// the new one as <c>settings.json.previous</c>.
/// </summary>
public sealed class StopHookRegistration
{
    private const string PreviousSuffix = ".previous";

    private static readonly JsonSerializerOptions Readable = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _hookPath;

    /// <summary>The registration in a Claude Code configuration folder, of the hook at <paramref name="hookPath"/>.</summary>
    public StopHookRegistration(string configFolder, string hookPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);

        _hookPath = hookPath;
        SettingsPath = Path.Combine(Path.GetFullPath(configFolder), "settings.json");
    }

    /// <summary>Claude Code's settings file.</summary>
    public string SettingsPath { get; }

    /// <summary>Whether any hook command in the settings runs the Theseus hook, as Help › About checks.</summary>
    public bool IsRegistered => HookInstallation.IsInstalled(Read());

    /// <summary>
    /// The entry that would be added, on one line, for the step to show before anything is asked: the step keeps its lines
    /// sparse (memory's dialog rules), and the confirmation shows it laid out in full.
    /// </summary>
    public string Entry => Entries().ToJsonString(Compact);

    /// <summary>The exact change, for the owner to read; null when registered already or the settings cannot be safely changed.</summary>
    public string? Change() =>
        IsRegistered || Editable(Read()) is null
            ? null
            : $"Add to hooks in {SettingsPath}:{Environment.NewLine}{Entries().ToJsonString(Readable)}";

    /// <summary>Adds the entry, keeping everything else in the settings and the old file beside it.</summary>
    public HoldoutGuardOutcome Add()
    {
        var text = Read();

        if (HookInstallation.IsInstalled(text))
        {
            return new HoldoutGuardOutcome(true, "The stop hook is registered already.");
        }

        if (Editable(text) is not { } settings)
        {
            return new HoldoutGuardOutcome(false,
                $"{SettingsPath} does not read as settings whose hooks can be added to, so nothing was changed. Add the entry by hand.");
        }

        var hooks = settings["hooks"] as JsonObject ?? new JsonObject();
        settings["hooks"] = hooks;

        foreach (var name in HookInstallation.Events)
        {
            var list = hooks[name] as JsonArray ?? new JsonArray();
            hooks[name] = list;
            list.Add(EventEntry());
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

            if (text is not null)
            {
                File.Copy(SettingsPath, SettingsPath + PreviousSuffix, overwrite: true);
            }

            var staged = SettingsPath + ".installing";
            File.WriteAllText(staged, settings.ToJsonString(Readable));
            File.Move(staged, SettingsPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HoldoutGuardOutcome(false, $"Could not write {SettingsPath}: {exception.Message}");
        }

        return new HoldoutGuardOutcome(true,
            $"Registered the stop hook in {SettingsPath}; the old file is kept beside it. Sessions started from now on run it.");
    }

    private JsonObject Entries()
    {
        var entries = new JsonObject();

        foreach (var name in HookInstallation.Events)
        {
            entries[name] = new JsonArray(EventEntry());
        }

        return entries;
    }

    // Quoted inside the command: a shell reads an unquoted C:\Program Files\... as two words (HookInstallation.Snippet).
    private JsonObject EventEntry() => new()
    {
        ["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command",
            ["command"] = $"\"{_hookPath}\"",
        }),
    };

    // The settings as an object the entries can be added to without losing anything, or null: unreadable, a duplicate key,
    // or hooks or one of its event lists of another shape, which a rewrite would replace.
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

            return settings["hooks"] switch
            {
                null => settings,
                JsonObject hooks when HookInstallation.Events.All(name => hooks[name] is null or JsonArray) => settings,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string? Read() => File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;
}
