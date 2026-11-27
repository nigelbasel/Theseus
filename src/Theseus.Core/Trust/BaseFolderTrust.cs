using Theseus.Core.Diagnostics;
using Theseus.Core.Settings;

namespace Theseus.Core.Trust;

/// <summary>
/// The base folders Theseus keeps trusted, one plain-folder record each (objective 1.3f, Nigel 2026-11-27: "Having a setting
/// that requires adding more and more trusted directories really does not work"). Claude Code walks up from a plain start
/// folder to the first trusted one, so a card that starts in a plain folder under a base folder is never asked; a card with
/// worktrees starts in its workstream folder for that reason. A repository's record is never written here: a base folder
/// whose key is not the folder itself (one inside a repository, or a worktree) is refused, as are a home folder, a drive's
/// root and a holdout.
/// </summary>
public sealed class BaseFolderTrust
{
    private readonly ClaudeFolderTrust _trust;
    private readonly ISettingsStore _settings;
    private readonly Func<IEnumerable<string>> _holdouts;
    private readonly string _home;
    private readonly HashSet<string> _refusalsSaid = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the trust over Claude Code's records and the owner's settings.</summary>
    /// <param name="trust">Claude Code's trust records, written with their own safe write.</param>
    /// <param name="settings">Where the base folders are kept.</param>
    /// <param name="holdouts">The holdout folders in force, and the folder Theseus's own holdouts sit in.</param>
    /// <param name="home">The home folder; null for this user's.</param>
    public BaseFolderTrust(ClaudeFolderTrust trust, ISettingsStore settings, Func<IEnumerable<string>> holdouts, string? home = null)
    {
        _trust = trust;
        _settings = settings;
        _holdouts = holdouts;
        _home = home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>The trust records themselves.</summary>
    public ClaudeFolderTrust Records => _trust;

    /// <summary>The base folders in force; none until they are set, so nothing is written before then.</summary>
    public IReadOnlyList<string> Folders => _settings.Read().BaseFolders ?? [];

    /// <summary>
    /// The defaults: the projects folder's root (the repository holding it, as Claude Code keys it) and the worktrees
    /// folder, each only when it is set and exists.
    /// </summary>
    public static IReadOnlyList<string> Defaults(TheseusSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var folders = new List<string>();

        if (Existing(settings.ProjectsFolder) is { } projects)
        {
            folders.Add(RepositoryRoot.MainOf(projects) is { } root ? Full(root) : projects);
        }

        if (Existing(settings.WorktreesFolder) is { } worktrees)
        {
            folders.Add(worktrees);
        }

        return [.. folders.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Sets the defaults on an install that never had base folders but has a projects or worktrees folder, and says whether
    /// it did, so the app shows its one banner. Nothing is written to Claude Code's file by this alone.
    /// </summary>
    public bool SetDefaultsOnUpgrade()
    {
        var settings = _settings.Read();

        if (settings.BaseFolders is not null || Defaults(settings) is not { Count: > 0 } defaults)
        {
            return false;
        }

        _settings.Write(settings with { BaseFolders = defaults });
        TheseusLog.Information("Folder trust: base folders set to their defaults on upgrade: {Folders}", string.Join(", ", defaults));

        return true;
    }

    /// <summary>Adds a base folder; true when it was not there already.</summary>
    public bool Add(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var full = Full(folder);
        var current = Folders;

        if (current.Contains(full, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        Write([.. current, full]);

        return true;
    }

    /// <summary>Puts the base folders back to their defaults.</summary>
    public void Reset() => Write(Defaults(_settings.Read()));

    /// <summary>Why a folder may never be a base folder, or null when it may.</summary>
    public string? Refusal(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var full = Full(folder);
        var key = Full(ClaudeFolderTrust.KeyOf(full));

        if (string.Equals(Path.GetPathRoot(full), full, StringComparison.OrdinalIgnoreCase))
        {
            return $"{full} is a drive's root";
        }

        // The home folder or anything holding it: a plain folder's trust covers its subfolders.
        if (_home.Length > 0 && Within(Full(_home), full))
        {
            return $"{full} is or holds the home folder";
        }

        var holdouts = _holdouts().Where(each => !string.IsNullOrWhiteSpace(each)).Select(Full).ToList();

        if (holdouts.Any(holdout => Within(full, holdout) || Within(holdout, full)) || NamedAsAHoldout(full))
        {
            return $"{full} is, holds or is inside a holdout folder";
        }

        if (!string.Equals(key, full, StringComparison.OrdinalIgnoreCase))
        {
            return $"{full} is inside the repository {key}, which Claude Code would trust whole";
        }

        return null;
    }

    /// <summary>
    /// Writes each base folder's record that is missing, at startup and before each launch. Refused folders are logged once
    /// and never written. Returns the keys written and any failure.
    /// </summary>
    public BaseTrustOutcome Keep()
    {
        var allowed = new List<string>();
        var refused = new List<string>();

        foreach (var folder in Folders.Where(Directory.Exists))
        {
            if (Refusal(folder) is { } reason)
            {
                refused.Add(folder);

                if (_refusalsSaid.Add(folder))
                {
                    TheseusLog.Warning("Folder trust: base folder {Folder} is not trusted: {Reason}", folder, reason);
                }

                continue;
            }

            allowed.Add(folder);
        }

        var needs = _trust.Needs(allowed).Where(need => allowed.Any(folder => string.Equals(Full(folder), Full(need.Key), StringComparison.OrdinalIgnoreCase))).ToList();

        if (needs.Count == 0)
        {
            return new BaseTrustOutcome([], refused, null);
        }

        var outcome = _trust.Trust(needs);
        var stillAsked = _trust.Needs(needs.Select(need => need.Folder)).Select(need => need.Key).ToHashSet(StringComparer.Ordinal);
        var trusted = needs.Select(need => need.Key).Where(key => !stillAsked.Contains(key)).ToList();

        foreach (var key in trusted)
        {
            TheseusLog.Information("Folder trust: trusted base folder {Key} in Claude Code", key);
        }

        if (!outcome.Succeeded)
        {
            TheseusLog.Warning("Folder trust: {Result}", outcome.Message);
        }

        return new BaseTrustOutcome(trusted, refused, outcome.Succeeded ? null : outcome.Message);
    }

    /// <summary>What Theseus writes, said once in the first-run setup and in Options.</summary>
    public string Explanation() =>
        $"Theseus keeps each base folder trusted in Claude Code by setting projects[\"<folder>\"].hasTrustDialogAccepted = true in {_trust.ConfigPath}, one record per base folder and nothing else. A session started anywhere under one is then never asked. It never trusts your home folder, a drive's root, a holdout, or a folder inside a repository. Undo takes back every record Theseus set and empties the base folders.";

    /// <summary>Takes back every record Theseus set and empties the base folders, so nothing is trusted again.</summary>
    public FolderTrustOutcome Undo()
    {
        Write([]);

        return _trust.CanUndo ? _trust.Undo() : new FolderTrustOutcome(true, "Emptied the base folders. Theseus had set no trust record.");
    }

    private void Write(IReadOnlyList<string> folders)
    {
        _settings.Write(_settings.Read() with { BaseFolders = folders });
        TheseusLog.Information("Folder trust: base folders are now {Folders}", folders.Count == 0 ? "none" : string.Join(", ", folders));
    }

    // The convention's holdout folder beside a clone, refused even before the holdout folders in force have been read.
    private static bool NamedAsAHoldout(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(part => part.EndsWith(Links.HomeRepo.HoldoutSuffix, StringComparison.OrdinalIgnoreCase));

    private static string? Existing(string setting) =>
        string.IsNullOrWhiteSpace(setting) || !Directory.Exists(setting) ? null : Full(setting);

    private static string Full(string path)
    {
        var full = Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));

        return string.Equals(Path.GetPathRoot(full), full, StringComparison.OrdinalIgnoreCase) ? full : Path.TrimEndingDirectorySeparator(full);
    }

    // Whether path is folder or inside it.
    private static bool Within(string path, string folder)
    {
        if (string.Equals(path, folder, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar;

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
