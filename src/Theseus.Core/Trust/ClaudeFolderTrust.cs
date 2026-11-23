using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Theseus.Core.Trust;

/// <summary>
/// Claude Code's folder trust, as its configuration file records it: <c>projects["&lt;folder&gt;"].hasTrustDialogAccepted</c>,
/// keyed by a repository's main root with forward slashes. A session started in, or adding, a folder with no such record
/// asks the owner to trust it, and for an added folder the answer holds for that session only, so a verifier asked on
/// every start (project-view-usability item 7, Nigel 2026-11-23: "this seems onerous"). Theseus sets the records at the
/// owner's one click, after showing the exact change, and keeps what it set so Undo takes back only that. Sessions never
/// write this file.
/// </summary>
public sealed class ClaudeFolderTrust
{
    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _undoPath;
    private ((DateTime, long) Stamp, JsonObject? Projects)? _cached;

    /// <summary>Creates the trust store over a Claude Code configuration file and the file Theseus keeps its undo in.</summary>
    /// <param name="configPath">Claude Code's configuration file.</param>
    /// <param name="undoPath">Where the records Theseus set are kept for Undo.</param>
    public ClaudeFolderTrust(string configPath, string undoPath)
    {
        ConfigPath = configPath;
        _undoPath = undoPath;
    }

    /// <summary>Claude Code's configuration file.</summary>
    public string ConfigPath { get; }

    /// <summary>Whether a change Theseus made can be taken back.</summary>
    public bool CanUndo => File.Exists(_undoPath);

    /// <summary>
    /// The configuration file this user's Claude Code reads: <c>.claude.json</c> in <c>CLAUDE_CONFIG_DIR</c> when that
    /// is set, and in the home folder otherwise.
    /// </summary>
    public static string UserConfigPath() =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } set
            ? Path.Combine(set, ".claude.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    /// <summary>The key Claude Code looks a folder's trust up by.</summary>
    public static string KeyOf(string folder) => Spelled(RepositoryRoot.MainOf(folder) ?? Path.GetFullPath(folder));

    /// <summary>
    /// The folders Claude Code will ask about, one need per record, in the order given. A folder counts as trusted when
    /// its key is, or any folder from it up to its working tree's root is (above the root when it is in no repository),
    /// as Claude Code's own lookup walks. Missing folders and an unreadable file give nothing to offer.
    /// </summary>
    public IReadOnlyList<FolderTrustNeed> Needs(IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        if (ReadProjects() is not { } projects)
        {
            return [];
        }

        var trusted = projects
            .Where(entry => entry.Value is JsonObject entryObject && entryObject["hasTrustDialogAccepted"]?.GetValueKind() == JsonValueKind.True)
            .Select(entry => Spelled(entry.Key))
            .ToHashSet(StringComparer.Ordinal);
        var needs = new List<FolderTrustNeed>();

        foreach (var folder in folders.Where(folder => !string.IsNullOrWhiteSpace(folder)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var key = KeyOf(folder);

            if (trusted.Contains(key) || LookedUp(folder).Any(trusted.Contains)
                || needs.Any(need => string.Equals(need.Key, key, StringComparison.Ordinal)))
            {
                continue;
            }

            needs.Add(new FolderTrustNeed(folder, key));
        }

        return needs;
    }

    /// <summary>The exact change trusting these would make, for the owner to read before agreeing to it.</summary>
    public string Change(IReadOnlyList<FolderTrustNeed> needs)
    {
        ArgumentNullException.ThrowIfNull(needs);
        var text = new StringBuilder();
        text.AppendLine($"In {ConfigPath}, Theseus will set:");
        text.AppendLine();

        foreach (var need in needs)
        {
            text.AppendLine($"  projects[\"{need.Key}\"].hasTrustDialogAccepted = true");

            if (!string.Equals(Spelled(need.Folder), need.Key, StringComparison.Ordinal))
            {
                text.AppendLine($"      for {need.Folder}, a worktree of that repository, and its other worktrees");
            }
        }

        text.AppendLine();
        text.AppendLine("Claude Code then treats each as if you had answered Yes to its trust question once. Nothing else in the file changes meaning. Undo in Options takes these back.");

        return text.ToString();
    }

    /// <summary>
    /// Sets the records, reading the file afresh just before writing and replacing it in one move, so a session's write
    /// a moment earlier is kept. Keeps which records it set, and whether each existed, for Undo.
    /// </summary>
    public FolderTrustOutcome Trust(IReadOnlyList<FolderTrustNeed> needs)
    {
        ArgumentNullException.ThrowIfNull(needs);

        if (needs.Count == 0)
        {
            return new FolderTrustOutcome(true, "Every folder Theseus starts sessions in is already trusted.");
        }

        if (ReadRoot() is not { } root)
        {
            return new FolderTrustOutcome(false, $"Nothing was changed: {ConfigPath} could not be read as Claude Code's configuration.");
        }

        var projects = root["projects"] as JsonObject ?? [];
        root["projects"] = projects;
        var undo = ReadUndo();

        var set = 0;

        foreach (var need in needs)
        {
            if (projects[need.Key] is JsonObject entry)
            {
                // Answered Yes in a tab since the list was read: his own answer, which Undo must never take back.
                if (entry["hasTrustDialogAccepted"]?.GetValueKind() == JsonValueKind.True)
                {
                    continue;
                }

                entry["hasTrustDialogAccepted"] = true;
                undo.Add(new JsonObject { ["key"] = need.Key, ["created"] = false });
            }
            else
            {
                projects[need.Key] = NewEntry();
                undo.Add(new JsonObject { ["key"] = need.Key, ["created"] = true });
            }

            set++;
        }

        // The undo record goes first: one for a change that then fails to land is harmless, a change without one is not.
        try
        {
            Replace(_undoPath, undo.ToJsonString(Written));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FolderTrustOutcome(false, $"Nothing was changed: {exception.Message}");
        }

        try
        {
            Replace(ConfigPath, root.ToJsonString(Written));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FolderTrustOutcome(false, $"Claude Code's configuration was not changed: {exception.Message}");
        }

        var left = Needs(needs.Select(need => need.Folder));

        return left.Count == 0
            ? new FolderTrustOutcome(true, set == 1 ? "Trusted 1 folder in Claude Code." : $"Trusted {set} folders in Claude Code.")
            : new FolderTrustOutcome(false, $"Written, but {left.Count} still read as untrusted: {string.Join(", ", left.Select(need => need.Key))}.");
    }

    /// <summary>
    /// Takes back what <see cref="Trust"/> set: removes the records it created while no session has written into them,
    /// and clears the flag on the rest. A Yes the owner gave in a tab after Theseus set the flag cannot be told apart from
    /// it, and is taken back too.
    /// </summary>
    public FolderTrustOutcome Undo()
    {
        if (!CanUndo)
        {
            return new FolderTrustOutcome(false, "Theseus has trusted no folder, so there is nothing to undo.");
        }

        if (ReadRoot() is not { } root)
        {
            return new FolderTrustOutcome(false, $"Nothing was changed: {ConfigPath} could not be read as Claude Code's configuration.");
        }

        var undo = ReadUndo();

        if (root["projects"] is JsonObject projects)
        {
            foreach (var item in undo.OfType<JsonObject>())
            {
                var key = item["key"]?.GetValue<string>();

                if (key is null || projects[key] is not JsonObject entry)
                {
                    continue;
                }

                if (item["created"]?.GetValue<bool>() == true && UnusedSince(entry))
                {
                    projects.Remove(key);
                }
                else
                {
                    entry["hasTrustDialogAccepted"] = false;
                }
            }
        }

        try
        {
            Replace(ConfigPath, root.ToJsonString(Written));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FolderTrustOutcome(false, $"Nothing was changed: {exception.Message}");
        }

        try
        {
            File.Delete(_undoPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new FolderTrustOutcome(false, $"Taken back, but the undo record could not be removed: {exception.Message}");
        }

        return new FolderTrustOutcome(true, undo.Count == 1 ? "Took back the 1 folder Theseus trusted." : $"Took back the {undo.Count} folders Theseus trusted.");
    }

    // Claude Code's spelling of a key: forward slashes, an upper-case drive letter, no trailing slash except on a drive's
    // root. Its lookup is an exact property read, so keys are compared exactly.
    private static string Spelled(string path)
    {
        var forward = path.Replace('\\', '/');

        if (forward.Length >= 2 && forward[1] == ':')
        {
            forward = char.ToUpperInvariant(forward[0]) + forward[1..];
        }

        return forward.Length > 3 ? forward.TrimEnd('/') : forward;
    }

    // The folders Claude Code walks for an inherited record: up to the working tree's root, or to the drive's.
    private static IEnumerable<string> LookedUp(string folder)
    {
        var stop = RepositoryRoot.WorkingTreeOf(folder) is { } tree ? Spelled(tree) : null;

        for (var current = new DirectoryInfo(folder); current is not null; current = current.Parent)
        {
            var spelled = Spelled(current.FullName);

            yield return spelled;

            if (stop is not null && string.Equals(spelled, stop, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }
        }
    }

    // Claude Code's own default record, with trust accepted, as it writes one when the owner answers Yes.
    private static JsonObject NewEntry() => new()
    {
        ["allowedTools"] = new JsonArray(),
        ["mcpContextUris"] = new JsonArray(),
        ["mcpServers"] = new JsonObject(),
        ["enabledMcpjsonServers"] = new JsonArray(),
        ["disabledMcpjsonServers"] = new JsonArray(),
        ["hasTrustDialogAccepted"] = true,
        ["hasClaudeMdExternalIncludesApproved"] = false,
        ["hasClaudeMdExternalIncludesWarningShown"] = false,
    };

    // A record Theseus created is removed by Undo only while no session has written anything else into it.
    private static bool UnusedSince(JsonObject entry) =>
        JsonNode.DeepEquals(entry, NewEntry());

    // Read on every Start, so the parsed file is kept until it changes on disk.
    private JsonObject? ReadProjects()
    {
        var info = new FileInfo(ConfigPath);
        var stamp = info.Exists ? (info.LastWriteTimeUtc, info.Length) : default;

        if (_cached is { } cached && cached.Stamp == stamp)
        {
            return cached.Projects;
        }

        var projects = ReadRoot() is { } root ? root["projects"] as JsonObject ?? [] : null;
        _cached = (stamp, projects);

        return projects;
    }

    private JsonObject? ReadRoot()
    {
        try
        {
            return File.Exists(ConfigPath) ? JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private JsonArray ReadUndo()
    {
        try
        {
            return File.Exists(_undoPath) ? JsonNode.Parse(File.ReadAllText(_undoPath)) as JsonArray ?? [] : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return [];
        }
    }

    private static void Replace(string path, string text)
    {
        var staged = path + ".theseus";

        try
        {
            File.WriteAllText(staged, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(staged, path, overwrite: true);
        }
        finally
        {
            File.Delete(staged);
        }
    }
}
