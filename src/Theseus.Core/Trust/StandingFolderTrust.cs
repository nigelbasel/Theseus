using System.Text;
using Theseus.Core.Diagnostics;
using Theseus.Core.Settings;

namespace Theseus.Core.Trust;

/// <summary>
/// Folder trust the owner gives once and Theseus keeps (objective 1.3, Nigel 2026-11-26: "It is not reasonable for me to
/// have to keep adding folders to the options... it will be impossible to keep track of all of the folders"). Every card on
/// a fresh worktree asked, because Claude Code keys a worktree's trust on its main clone and nothing trusts a folder tree
/// up front. With the owner's standing consent on, Theseus writes each missing record before a launch, by the key Claude
/// Code looks up. The consent reaches only the projects folder and the clones folder: anything else is still listed in
/// Options for a click, and a home folder, a drive's root or a holdout is never trusted this way.
/// </summary>
public sealed class StandingFolderTrust
{
    private readonly ClaudeFolderTrust _trust;
    private readonly ISettingsStore _settings;
    private readonly Func<IEnumerable<string>> _holdouts;
    private readonly string _home;

    /// <summary>Creates the standing trust over Claude Code's records and the owner's settings.</summary>
    /// <param name="trust">Claude Code's trust records, written with their own safe write.</param>
    /// <param name="settings">Where the consent and the projects and clones folders are kept.</param>
    /// <param name="holdouts">The holdout folders in force, and the folder Theseus's own holdouts sit in.</param>
    /// <param name="home">The home folder; null for this user's.</param>
    public StandingFolderTrust(ClaudeFolderTrust trust, ISettingsStore settings, Func<IEnumerable<string>> holdouts, string? home = null)
    {
        _trust = trust;
        _settings = settings;
        _holdouts = holdouts;
        _home = home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Whether the owner's consent is on.</summary>
    public bool IsOn => _settings.Read().TrustCardFolders;

    /// <summary>The trust records themselves.</summary>
    public ClaudeFolderTrust Records => _trust;

    /// <summary>Records the owner's consent; nothing is written to Claude Code's file by this alone.</summary>
    public void SwitchOn() => Set(true);

    /// <summary>Withdraws the consent. The records already set stay until Undo.</summary>
    public void SwitchOff() => Set(false);

    /// <summary>
    /// Trusts what these folders are missing, before a launch, while the consent is on. Writes only the records the
    /// consent reaches, and returns the rest for Options to offer.
    /// </summary>
    public StandingTrustOutcome Keep(IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        if (!IsOn)
        {
            return StandingTrustOutcome.Nothing;
        }

        var (reached, left) = Split(_trust.Needs(folders));

        if (reached.Count == 0)
        {
            return new StandingTrustOutcome([], left, null);
        }

        var outcome = _trust.Trust(reached);
        var stillAsked = _trust.Needs(reached.Select(need => need.Folder)).Select(need => need.Key).ToHashSet(StringComparer.Ordinal);
        var trusted = reached.Select(need => need.Key).Where(key => !stillAsked.Contains(key)).ToList();

        foreach (var need in reached.Where(need => trusted.Contains(need.Key)))
        {
            TheseusLog.Information("Folder trust: trusted {Key} in Claude Code for {Folder}, under the standing consent", need.Key, need.Folder);
        }

        if (!outcome.Succeeded)
        {
            TheseusLog.Warning("Folder trust: {Result}", outcome.Message);
        }

        return new StandingTrustOutcome(trusted, left, outcome.Succeeded ? null : outcome.Message);
    }

    /// <summary>Of the folders Claude Code would ask about, those the consent does not reach.</summary>
    public IReadOnlyList<FolderTrustNeed> LeftForAClick(IEnumerable<string> folders) => Split(_trust.Needs(folders)).Left;

    /// <summary>
    /// The consent, shown once before it is switched on: what it writes, where, now and later, and what it never trusts.
    /// </summary>
    public string Consent(IEnumerable<string> folders)
    {
        var settings = _settings.Read();
        var (reached, _) = Split(_trust.Needs(folders));
        var text = new StringBuilder();
        text.AppendLine("Trust the folders my cards use, as they need it.");
        text.AppendLine();
        text.AppendLine("Before each card starts, Theseus will trust its folders in Claude Code, so its tab opens without asking. Claude Code keys a folder in a repository by that repository's root, and a worktree by its main clone, so one record covers every worktree of a repository.");
        text.AppendLine();
        text.AppendLine($"Theseus writes only in {_trust.ConfigPath}, setting projects[\"<key>\"].hasTrustDialogAccepted = true, and only for keys in:");
        text.AppendLine($"  the projects folder, {Shown(settings.ProjectsFolder)}");
        text.AppendLine($"  the clones folder, {Shown(settings.ClonesFolder)}");
        text.AppendLine();
        text.AppendLine("It never trusts your home folder, a drive's root, or a holdout folder. Anything else a card uses is still listed in Options for you to trust or not.");

        if (reached.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("For the cards on the board now, it will set:");

            foreach (var need in reached)
            {
                text.AppendLine($"  projects[\"{need.Key}\"].hasTrustDialogAccepted = true");
            }
        }

        text.AppendLine();
        text.AppendLine("Undo in Options takes back every record Theseus set and switches this off.");

        return text.ToString();
    }

    /// <summary>Takes back every record Theseus set, and switches the consent off so the next start does not set them again.</summary>
    public FolderTrustOutcome Undo()
    {
        SwitchOff();

        return _trust.Undo();
    }

    private void Set(bool on)
    {
        var settings = _settings.Read();

        if (settings.TrustCardFolders != on)
        {
            _settings.Write(settings with { TrustCardFolders = on });
            TheseusLog.Information("Folder trust: the standing consent is {State}", on ? "on" : "off");
        }
    }

    private (IReadOnlyList<FolderTrustNeed> Reached, IReadOnlyList<FolderTrustNeed> Left) Split(IReadOnlyList<FolderTrustNeed> needs)
    {
        if (needs.Count == 0)
        {
            return ([], []);
        }

        var settings = _settings.Read();
        var holdouts = _holdouts().Where(folder => !string.IsNullOrWhiteSpace(folder)).Select(Full).ToList();
        var reached = new List<FolderTrustNeed>();
        var left = new List<FolderTrustNeed>();

        foreach (var need in needs)
        {
            (Reaches(need, settings, holdouts) ? reached : left).Add(need);
        }

        return (reached, left);
    }

    private bool Reaches(FolderTrustNeed need, TheseusSettings settings, IReadOnlyList<string> holdouts)
    {
        var key = Full(need.Key);
        var folder = Full(need.Folder);

        if (string.Equals(Path.GetPathRoot(key), key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // The home folder or anything holding it: a plain folder's trust covers its subfolders.
        if (_home.Length > 0 && Within(Full(_home), key))
        {
            return false;
        }

        if (holdouts.Any(holdout => Within(key, holdout) || Within(holdout, key) || Within(folder, holdout))
            || NamedAsAHoldout(key) || NamedAsAHoldout(folder))
        {
            return false;
        }

        if (Folder(settings.ProjectsFolder) is { } projects
            && (Within(key, projects) || string.Equals(key, Full(ClaudeFolderTrust.KeyOf(projects)), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Strictly inside: a plain clones folder trusted whole would cover every folder in it that is not a repository.
        return Folder(settings.ClonesFolder) is { } clones && Within(key, clones) && !string.Equals(key, clones, StringComparison.OrdinalIgnoreCase);
    }

    // The convention's holdout folder beside a clone, refused even before the holdout folders in force have been read.
    private static bool NamedAsAHoldout(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(part => part.EndsWith(Links.HomeRepo.HoldoutSuffix, StringComparison.OrdinalIgnoreCase));

    private static string? Folder(string setting) =>
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

    private static string Shown(string setting) => string.IsNullOrWhiteSpace(setting) ? "not chosen, so nothing there" : setting;
}
