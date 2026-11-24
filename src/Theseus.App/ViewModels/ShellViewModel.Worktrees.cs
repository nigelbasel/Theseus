using System.IO;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Core.Worktrees;

namespace Theseus.App.ViewModels;

/// <summary>
/// A worktree per repo for each workstream, one branch name across its repos (objective 2.1, worktree-per-repo): made
/// before a card's session opens, from its project row, and offered for removal, clean ones only, when it is archived.
/// </summary>
public sealed partial class ShellViewModel
{
    private IGit? _git;

    /// <summary>The git every worktree is made and removed with; settable so a test can watch it.</summary>
    internal IGit Git
    {
        get => _git ??= new GitProcess();
        set => _git = value;
    }

    /// <summary>
    /// The card with its repos ready, or null with the refusal added when it must not start (AC 7, 9). A card in no
    /// project, on a trunk or none, or with either folder unset, comes back as it went in (AC 1).
    /// </summary>
    private Workstream? WithWorktrees(Workstream workstream, List<string> refusals, List<string> notes)
    {
        if (WorktreeRequestFor(workstream) is not { } request)
        {
            return workstream;
        }

        var prepared = new WorktreePreparer(Git).Prepare(request);

        if (!prepared.MayStart)
        {
            TheseusLog.Information("Refused to start {Workstream}: {Reason}", workstream.Key, prepared.Refusal);
            refusals.Add($"{workstream.Title} did not start: {prepared.Refusal}");

            return null;
        }

        if (prepared.Repos.Count == 0)
        {
            return workstream;
        }

        if (prepared.Notice is { } notice)
        {
            notes.Add($"{workstream.Title} {notice}.");
        }

        var added = prepared.Folders
            .Where(folder => !workstream.AddDirectories.Any(existing => SameFolder(existing, folder)))
            .ToList();
        var ready = workstream with
        {
            AddDirectories = [.. workstream.AddDirectories, .. added],
            StartInstruction = string.IsNullOrWhiteSpace(workstream.StartInstruction)
                ? prepared.Opening
                : $"{workstream.StartInstruction}{Environment.NewLine}{Environment.NewLine}{prepared.Opening}",
        };

        if (added.Count > 0)
        {
            if (_plans.TryRead(workstream.Key) is { } plan)
            {
                _plans.Write(workstream.Key, plan with { AddDirs = [.. plan.AddDirs, .. added.Where(folder => !plan.AddDirs.Any(existing => SameFolder(existing, folder)))] });
            }

            if (Board.Card(workstream.Key) is { } card)
            {
                card.Workstream = card.Workstream with { AddDirectories = ready.AddDirectories };
                Board.Persist(card);
            }

            TheseusLog.Information("Added {Count} worktree folder(s) to {Workstream}'s extra directories: {Folders}",
                added.Count, workstream.Key, string.Join(", ", added));
        }

        return ready;
    }

    /// <summary>What a start needs to prepare a card's worktrees, or null when it is in no readable project or has no row.</summary>
    private WorktreeRequest? WorktreeRequestFor(Workstream workstream)
    {
        if (workstream is not { Project: { } project, Slug: { } slug } || Readable(project) is not { File: { } file } reading)
        {
            return null;
        }

        var row = file.Workstreams.LastOrDefault(each => string.Equals(each.Slug, slug, StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            return null;
        }

        var settings = _settings.Read();

        return new WorktreeRequest(row.Slug, row.Branch, row.Repos, reading.Text ?? ProjectText(reading.Folder), project,
            file.PrimaryBranch, settings.ClonesFolder, settings.WorktreesFolder);
    }

    /// <summary>Whether another card's folder is the worktree, inside it, or holds it, so removing it would pull work from under that card.</summary>
    private static bool Overlaps(string? other, string worktree)
    {
        if (string.IsNullOrWhiteSpace(other))
        {
            return false;
        }

        try
        {
            var a = ListedWorktree.Normal(other) + Path.DirectorySeparatorChar;
            var b = ListedWorktree.Normal(worktree) + Path.DirectorySeparatorChar;

            return a.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string? ProjectText(string? folder)
    {
        try
        {
            return folder is not null && File.Exists(Path.Combine(folder, "project.md")) ? File.ReadAllText(Path.Combine(folder, "project.md")) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// After a card is archived, offers its clean worktrees for removal as a checklist (AC 14 to 17). The archive stands
    /// whatever is answered; a worktree with changes is named and kept; no branch is ever deleted. Returns the notice to
    /// add, or null when nothing was offered.
    /// </summary>
    private string? OfferWorktreeRemoval(IReadOnlyList<WorkstreamCardViewModel> archived)
    {
        var settings = _settings.Read();

        if (string.IsNullOrWhiteSpace(settings.WorktreesFolder) || AskChecklist is not { } ask)
        {
            return null;
        }

        var archivedKeys = archived.Select(card => card.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = Board.Cards.Where(card => !card.Workstream.IsArchived && !archivedKeys.Contains(card.Key))
            .SelectMany(card => card.Workstream.AddDirectories.Append(card.Workstream.WorkingDirectory))
            .ToList();
        var clearUp = new WorktreeClearUp(Git);
        var clean = new List<RemovableWorktree>();
        var kept = new List<RemovableWorktree>();

        foreach (var card in archived)
        {
            if (WorktreeRequestFor(card.Workstream) is not { } request)
            {
                continue;
            }

            var found = clearUp.Find(settings.WorktreesFolder, request.Slug, request.Branch,
                folder => others.Any(other => Overlaps(other, folder)));
            clean.AddRange(found.Clean);
            kept.AddRange(found.Kept);
        }

        if (clean.Count == 0)
        {
            return kept.Count == 0 ? null : $" Kept {Checklists.Folders(kept)}: uncommitted changes or ignored files.";
        }

        var ticked = ask(new ChecklistViewModel(Checklists.RemoveWorktrees(clean, kept)));

        if (ticked is not { Count: > 0 })
        {
            return " No worktrees were removed.";
        }

        var removed = new List<string>();
        var refused = new List<string>();

        foreach (var worktree in clean.Where(each => ticked.Contains(each.Folder, StringComparer.OrdinalIgnoreCase)))
        {
            if (clearUp.Remove(worktree) is { } why)
            {
                refused.Add(why);
            }
            else
            {
                removed.Add(worktree.Folder);
            }
        }

        return $" Removed {removed.Count} {(removed.Count == 1 ? "worktree" : "worktrees")}; every branch is kept."
               + (refused.Count > 0 ? $" {string.Join(" ", refused)}" : string.Empty)
               + (kept.Count > 0 ? $" Kept {Checklists.Folders(kept)}: uncommitted changes or ignored files." : string.Empty);
    }
}
