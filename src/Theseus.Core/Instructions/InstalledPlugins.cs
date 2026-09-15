using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Instructions;

/// <summary>
/// The plugins a session gets: enabled in <c>~/.claude/settings.json</c> and installed according to
/// <c>~/.claude/plugins/installed_plugins.json</c>. The plugin cache holds far more than that - old
/// versions and whole marketplaces - so the cache itself says nothing about what is in use.
/// Plugins enabled only in a project's own settings are not included.
/// </summary>
public static class InstalledPlugins
{
    private static readonly JsonDocumentOptions Lenient = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>The enabled plugins, in the order the settings name them.</summary>
    public static IReadOnlyList<InstalledPlugin> Read(string userClaudeDirectory)
    {
        using var settings = Parse(Path.Combine(userClaudeDirectory, "settings.json"));
        using var installed = Parse(Path.Combine(userClaudeDirectory, "plugins", "installed_plugins.json"));

        if (settings is null || installed is null
            || !settings.RootElement.TryGetProperty("enabledPlugins", out var enabled)
            || enabled.ValueKind != JsonValueKind.Object
            || !installed.RootElement.TryGetProperty("plugins", out var plugins)
            || plugins.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<InstalledPlugin>();

        foreach (var switched in enabled.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.True))
        {
            if (plugins.TryGetProperty(switched.Name, out var versions)
                && versions.ValueKind == JsonValueKind.Array
                && versions.GetArrayLength() > 0
                && versions[0].TryGetProperty("installPath", out var path)
                && path.GetString() is { Length: > 0 } installPath)
            {
                result.Add(new InstalledPlugin(switched.Name.Split('@')[0], installPath));
            }
        }

        return result;
    }

    private static JsonDocument? Parse(string path)
    {
        var text = DirectoryWalk.Text(path);

        if (text.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(text, Lenient);
        }
        catch (JsonException exception)
        {
            TheseusLog.Debug("Could not read {Path} as JSON: {Reason}", path, exception.Message);

            return null;
        }
    }
}
