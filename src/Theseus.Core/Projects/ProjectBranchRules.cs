using System.Text.RegularExpressions;
using Theseus.Core.Proposals;
using Theseus.Core.Settings;

namespace Theseus.Core.Projects;

/// <summary>
/// The branch rules the owner grants a project (1.1e AC 12b; Nigel, 2026-11-21, after a project's card stopped for every
/// push): while he has ticked the box for a project in Project properties, its cards are told at each start that they may
/// branch off its primary branch, push, and merge back, in place of the brief's "authorises no … branch, push" line. The
/// grant is kept in Theseus's settings, pinned to the primary branch he granted it for, never read from project.md, which
/// sessions write.
/// </summary>
public static partial class ProjectBranchRules
{
    /// <summary>What a card's mark says.</summary>
    public const string Mark = "project branch rules";

    /// <summary>What a resumed session is told when the rules it was given are withdrawn (review of PR 2c, finding 3).</summary>
    public const string Withdrawn =
        "Your project's branch rules are withdrawn: ask the user before any branch, worktree, push or pull request.";

    /// <summary>What is added before rules that follow an opening, so an edited no-branch line does not contradict them.</summary>
    public const string Replaces = "The project branch rules below replace any earlier line saying you may not branch or push.";

    private static readonly string[] Trunks = ["main", "master", "trunk", "develop", "HEAD"];

    /// <summary>
    /// Whether a primary branch can carry a grant: a plain branch name that is not a trunk. A file with none, <c>none</c>,
    /// a placeholder, a trunk, or anything git would not take as a branch name grants nothing (review, finding 2).
    /// </summary>
    public static bool HasPrimary(string? primary)
    {
        var name = primary?.Trim() ?? string.Empty;

        return name.Length > 0
               && !string.Equals(name, "none", StringComparison.OrdinalIgnoreCase)
               && !Trunks.Contains(name, StringComparer.OrdinalIgnoreCase)
               && BranchName().IsMatch(name)
               && !name.Contains("..", StringComparison.Ordinal)
               && !name.Contains("//", StringComparison.Ordinal)
               && !name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The primary branch a card may work from, or null: only while the owner's grant for its project is in the settings
    /// and the project file, as read now, names the same primary branch he granted it for. Nothing the file says can grant
    /// it or widen it.
    /// </summary>
    /// <param name="project">The card's project, or null for a card in no project.</param>
    /// <param name="grants">The owner's grants, by project folder name, from Theseus's settings.</param>
    /// <param name="primaryOf">The primary branch a project's file names now, or null.</param>
    public static string? GrantedPrimary(string? project, IReadOnlyDictionary<string, ProjectBranchGrant> grants, Func<string, string?> primaryOf)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(primaryOf);

        return GrantOf(project, grants) is { } grant
               && primaryOf(project!)?.Trim() is { } primary
               && string.Equals(primary, grant.Primary, StringComparison.Ordinal)
               && HasPrimary(primary)
            ? primary
            : null;
    }

    /// <summary>The grant for a project, by its folder name, or null.</summary>
    public static ProjectBranchGrant? GrantOf(string? project, IReadOnlyDictionary<string, ProjectBranchGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        return project is null
            ? null
            : grants.FirstOrDefault(grant => string.Equals(grant.Key, project, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>The text a card of a granted project is told at its start, naming the primary branch and its own branch.</summary>
    /// <param name="primary">The project's primary branch.</param>
    /// <param name="ownName">The card's own branch's last part: its records folder's name, as its row's branch is (review, finding 1).</param>
    public static string Text(string primary, string ownName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primary);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownName);

        var owner = primary.Trim().LastIndexOf('/') is var slash and > 0 ? primary.Trim()[..(slash + 1)] : string.Empty;

        return $"Project branch rules (granted by the owner for this project): you may create your own branch {owner}{ownName.Trim()} off "
               + $"{primary.Trim()} and its worktree, commit, push it, and merge it into {primary.Trim()} once your full test suite "
               + "passes on the merge result and your review is done. Ask the user before any Jira item, any pull request into "
               + "the trunk, a force push, deleting a branch, and anything outside this project's repositories.";
    }

    /// <summary>
    /// The text for the kind of rules granted (2.4 W2): the first kind as <see cref="Text(string, string)"/>; for item
    /// branches, a branch per item off its repository's trunk, pushed, with a pull request the owner completes. The trunk
    /// rules are said whatever the project's file says, and the tracker states a card may move an item to are read from the
    /// tracker, never written here.
    /// </summary>
    public static string Text(string primary, string ownName, BranchGrantKind kind)
    {
        if (kind == BranchGrantKind.MergeIntoPrimary)
        {
            return Text(primary, ownName);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(primary);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownName);

        var owner = primary.Trim().LastIndexOf('/') is var slash and > 0 ? primary.Trim()[..(slash + 1)] : string.Empty;

        return $"Project branch rules (granted by the owner for this project): for the item you work on, you may create your own "
               + $"branch {owner}{ownName.Trim()} off its repository's trunk, with its worktree, commit, push that branch, and raise "
               + "a pull request from it into the trunk. Never complete, merge, approve, auto-complete or abandon a pull request "
               + "into the trunk, and never push to the trunk: those are the owner's, whatever the project's file says. Your "
               + "project's Decisions table in project.md says what you may do on its tracker; the states you may move an item to "
               + "are the ones its tracker offers it now, so read them from the tracker (its transitions) before moving it, never "
               + "from memory. Ask the user before creating any tracker item, a force push, deleting a branch, and anything "
               + "outside this project's repositories.";
    }

    /// <summary>
    /// The opening a card is given: the brief's line that authorises no branch replaced by the rules, or the rules added
    /// at the end, saying they replace any such line, when the opening has none; unchanged without rules.
    /// </summary>
    public static string Apply(string opening, string? rules)
    {
        ArgumentNullException.ThrowIfNull(opening);

        if (string.IsNullOrWhiteSpace(rules))
        {
            return opening;
        }

        return opening.Contains(ProposalPrompt.NoBranchLine, StringComparison.Ordinal)
            ? opening.Replace(ProposalPrompt.NoBranchLine, rules, StringComparison.Ordinal)
            : $"{opening.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{Replaces} {rules}";
    }

    /// <summary>
    /// A start's two openings with the rules (review, findings 3 and 5): the full one, which a fresh start uses, and the one
    /// sent, which for a resume is its one-line opening. A resume given the rules at its last start, and given none now, is
    /// told they are withdrawn, since its conversation still holds them.
    /// </summary>
    /// <param name="full">The full opening.</param>
    /// <param name="resume">The resume opening, or null for a fresh start.</param>
    /// <param name="rules">The rules this start, or null.</param>
    /// <param name="toldBefore">Whether the card was given the rules at its last start.</param>
    public static (string Full, string Sent) Openings(string full, string? resume, string? rules, bool toldBefore)
    {
        ArgumentNullException.ThrowIfNull(full);

        var withRules = Apply(full, rules);

        if (resume is null)
        {
            return (withRules, withRules);
        }

        var sent = rules is not null
            ? Apply(resume, rules)
            : toldBefore
                ? $"{resume.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{Withdrawn}"
                : resume;

        return (withRules, sent);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._/-]*[A-Za-z0-9_]$")]
    private static partial Regex BranchName();
}
