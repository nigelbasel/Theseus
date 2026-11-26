using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Theseus.Core.Guards;

/// <summary>
/// Auto mode's <c>autoMode.environment</c> in Claude Code's settings: the prose its classifier reads to tell routine work
/// from leaving the trust boundary. Built from the template this build ships and what the owner entered, and written only
/// at his click after the exact change is shown (guards-ship-with-theseus brief step 3, Nigel 2026-11-26). A block written
/// for one checkout that said it had no remotes made the classifier refuse every push, about six hours of waits overnight.
/// </summary>
public sealed class AutoModeEnvironment
{
    private const string PreviousSuffix = ".previous";
    private const string Resource = "Theseus.Core.AutoMode.environment.template.txt";

    private static readonly JsonSerializerOptions Readable = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Lazy<IReadOnlyList<string>> Template = new(ReadTemplate);

    /// <summary>Auto mode's environment in a Claude Code configuration folder.</summary>
    /// <param name="configFolder">The Claude Code configuration folder.</param>
    public AutoModeEnvironment(string configFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);

        SettingsPath = Path.Combine(Path.GetFullPath(configFolder), "settings.json");
    }

    /// <summary>Claude Code's settings file.</summary>
    public string SettingsPath { get; }

    /// <summary>
    /// The environment's lines: the template's, with the owner's entries where it names them. A line whose entry is empty
    /// says None configured, as Claude Code's own block does.
    /// </summary>
    public static IReadOnlyList<string> Build(AutoModeEntries entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var values = new Dictionary<string, string>
        {
            ["{sourceControlHosts}"] = Listed(entries.SourceControlHosts, "and", quoted: false),
            ["{internalDomains}"] = Listed(entries.InternalDomains, "and", quoted: false),
            ["{productionPatterns}"] = Listed(entries.ProductionPatterns, "or", quoted: true),
        };

        return [.. Template.Value.Select(line => Filled(line, values))];
    }

    /// <summary>Whether the settings hold the environment these entries build.</summary>
    public bool Matches(AutoModeEntries entries) => Current() is { } current && current.SequenceEqual(Build(entries));

    /// <summary>The exact change, for the owner to read before it is made; null when nothing would change or the settings cannot be changed safely.</summary>
    public string? Change(AutoModeEntries entries)
    {
        if (Matches(entries) || Editable(Read()) is null)
        {
            return null;
        }

        var built = Json(Build(entries));

        return Current() is { } current
            ? $"In {SettingsPath}, replace autoMode.environment:{Environment.NewLine}{Json(current)}{Environment.NewLine}with:{Environment.NewLine}{built}"
            : $"Set autoMode.environment in {SettingsPath} to:{Environment.NewLine}{built}";
    }

    /// <summary>Writes the environment these entries build, changing nothing else and keeping the old file as <c>settings.json.previous</c>.</summary>
    public GuardOutcome Write(AutoModeEntries entries)
    {
        var text = Read();

        if (Editable(text) is not { } settings)
        {
            return new GuardOutcome(false,
                $"{SettingsPath} does not read as settings with an autoMode object, so nothing was changed. Set the environment by hand.");
        }

        var autoMode = settings["autoMode"] as JsonObject ?? new JsonObject();
        settings["autoMode"] = autoMode;
        autoMode["environment"] = new JsonArray([.. Build(entries).Select(line => (JsonNode?)JsonValue.Create(line))]);

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
            return new GuardOutcome(false, $"Could not write {SettingsPath}: {exception.Message}");
        }

        return new GuardOutcome(true,
            $"Wrote auto mode's environment to {SettingsPath}; the old file is kept beside it. Sessions started from now on read it.");
    }

    /// <summary>The environment's lines as the settings hold them, or null when there are none to read.</summary>
    public IReadOnlyList<string>? Current()
    {
        try
        {
            return Read() is { } text && JsonNode.Parse(text)?["autoMode"]?["environment"] is JsonArray lines
                ? [.. lines.Select(line => line?.GetValueKind() == JsonValueKind.String ? line.GetValue<string>() : string.Empty)]
                : null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string Filled(string line, IReadOnlyDictionary<string, string> values)
    {
        foreach (var (token, value) in values)
        {
            if (!line.Contains(token, StringComparison.Ordinal))
            {
                continue;
            }

            if (value.Length == 0)
            {
                var key = line.IndexOf("**: ", StringComparison.Ordinal);

                return key < 0 ? line.Replace(token, "none", StringComparison.Ordinal) : line[..(key + 4)] + "None configured";
            }

            line = line.Replace(token, value, StringComparison.Ordinal);
        }

        return line;
    }

    // "a", "a and b", "a, b and c"; patterns in backticks, as Claude Code's own block writes them.
    private static string Listed(IReadOnlyList<string> items, string conjunction, bool quoted)
    {
        var words = items.Select(item => item.Trim()).Where(item => item.Length > 0).Select(item => quoted ? $"`{item}`" : item).ToList();

        return words.Count switch
        {
            0 => string.Empty,
            1 => words[0],
            _ => $"{string.Join(", ", words[..^1])} {conjunction} {words[^1]}",
        };
    }

    // The settings as an object the environment can be set in without losing anything, or null: unreadable, a duplicate
    // key, or autoMode of another shape, which a rewrite would replace.
    private static JsonObject? Editable(string? text)
    {
        if (text is null)
        {
            return new JsonObject();
        }

        try
        {
            using var strict = JsonDocument.Parse(text, new JsonDocumentOptions { AllowDuplicateProperties = false });

            return JsonNode.Parse(text) is JsonObject settings && settings["autoMode"] is null or JsonObject ? settings : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Json(IReadOnlyList<string> lines) =>
        new JsonArray([.. lines.Select(line => (JsonNode?)JsonValue.Create(line))]).ToJsonString(Readable);

    private string? Read() => File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;

    private static IReadOnlyList<string> ReadTemplate()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"{Resource} is not in this build");
        using var reader = new StreamReader(stream);

        return [.. reader.ReadToEnd().Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];
    }
}
