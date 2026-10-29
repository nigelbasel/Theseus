using System.Text.RegularExpressions;
using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Proposals;

/// <summary>
/// What a proposed workstream must pass before it reaches the board, and what Nigel is warned about
/// when it does. A refusal is for what would let a session reach past its own card: a brief that is a
/// handoff, a Jira key it does not hold, a chain more than two deep. Everything that is only worth a
/// look - a title like an existing card's, a brief that mentions a branch or a push - is a warning,
/// because Nigel decides.
/// </summary>
public static partial class ProposalCheck
{
    /// <summary>How many hand-overs below a root a proposer may be. A child may propose; a grandchild may not.</summary>
    public const int MaximumProposerDepth = 1;

    /// <summary>The longest brief read for warnings. A brief is a page or two; past this it is not read at all.</summary>
    private const long LongestBriefRead = 256 * 1024;

    /// <summary>Checks a proposal from <paramref name="requesterKey"/> against the board as it stands.</summary>
    /// <param name="request">What was asked for.</param>
    /// <param name="requesterKey">The proposing workstream.</param>
    /// <param name="board">The board as of its last refresh.</param>
    /// <param name="links">Every link ever made, removed ones included, so removing a link cannot reset the depth.</param>
    /// <param name="paths">What a session may not name, or null to skip that rule.</param>
    /// <param name="briefFolderName">The board's brief folder, relative to the proposer's working directory.</param>
    public static ProposalCheckResult Check(ProposalRequest request, string requesterKey, BoardSnapshot board,
        IReadOnlyList<Link> links, SessionPathRule? paths, string briefFolderName)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(links);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ProposalCheckResult.Refused("title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DoneCriteria))
        {
            return ProposalCheckResult.Refused("done_criteria is required: what the new workstream must produce to be finished.");
        }

        if (board.Card(requesterKey) is not { } requester)
        {
            return ProposalCheckResult.Refused($"{requesterKey} is not on the board, so it cannot propose a workstream.");
        }

        if (requester.Workstream.IsArchived)
        {
            return ProposalCheckResult.Refused($"{requester.Title} is archived, so it cannot propose a workstream.");
        }

        if (string.IsNullOrWhiteSpace(request.Brief) || !Path.IsPathFullyQualified(request.Brief.Trim()))
        {
            return ProposalCheckResult.Refused("brief must be the absolute path of a file you have already written.");
        }

        var brief = request.Brief.Trim();

        if (!File.Exists(brief))
        {
            return ProposalCheckResult.Refused($"{brief} does not exist. Write the brief first, then propose.");
        }

        foreach (var path in new[] { brief, request.ExpectedOutput?.Trim() }.OfType<string>().Where(path => path.Length > 0))
        {
            if (paths?.Refusal(path, board) is { } refusal)
            {
                return ProposalCheckResult.Refused(refusal);
            }
        }

        if (request.ExpectedOutput is { } output && output.Trim().Length > 0 && !Path.IsPathFullyQualified(output.Trim()))
        {
            return ProposalCheckResult.Refused("expected_output must be an absolute path, or left out.");
        }

        var held = new HashSet<string>(requester.Workstream.JiraKeys, StringComparer.OrdinalIgnoreCase);

        if (request.JiraKeys.FirstOrDefault(key => !held.Contains(key.Trim())) is { } stranger)
        {
            return ProposalCheckResult.Refused(
                $"{stranger} is not on {requester.Title}'s card. A new workstream can carry only issues its parent already holds.");
        }

        if (Depth(requesterKey, links) > MaximumProposerDepth)
        {
            return ProposalCheckResult.Refused(
                $"{requester.Title} is already two hand-overs below where its chain started, so it cannot propose another. "
                + "Tell the user what you would have proposed; they can make it on the board.");
        }

        return new ProposalCheckResult(null, Warnings(request, brief, requester, board, briefFolderName));
    }

    /// <summary>How many hand-overs lead down to this card, counting removed links and archived ancestors.</summary>
    public static int Depth(string key, IReadOnlyList<Link> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key };
        var depth = 0;
        var current = key;

        while (links.Where(link => link.Kind == LinkKind.HandsOver && Same(link.To, current))
                   .OrderBy(link => link.IsActive ? 0 : 1)
                   .FirstOrDefault() is { } parent
               && seen.Add(parent.From))
        {
            depth++;
            current = parent.From;
        }

        return depth;
    }

    private static List<string> Warnings(ProposalRequest request, string brief, BoardSnapshotCard requester,
        BoardSnapshot board, string briefFolderName)
    {
        var warnings = new List<string>();
        var title = request.Title.Trim();
        var key = WorkstreamKey.FromTitle(title);

        foreach (var card in board.Cards.Where(card => !card.Workstream.IsArchived))
        {
            if (string.Equals(card.Title.Trim(), title, StringComparison.OrdinalIgnoreCase) || Same(card.Key, key))
            {
                warnings.Add($"Looks like the existing card {card.Title} ({card.Key}).");
            }
            else if (card.Workstream.Prompt.Contains(brief, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{card.Title} ({card.Key}) already starts from this brief.");
            }
        }

        var folder = Path.GetFullPath(Path.Combine(requester.Workstream.WorkingDirectory,
            string.IsNullOrWhiteSpace(briefFolderName) ? BriefFolder.DefaultName : briefFolderName.Trim()));

        foreach (var path in new[] { brief, request.ExpectedOutput?.Trim() }.OfType<string>().Where(path => path.Length > 0))
        {
            if (!Path.GetFullPath(path).StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{path} is outside the brief folder, {folder}.");
            }
        }

        var text = $"{BriefText(brief)}\n{request.DoneCriteria}";

        foreach (var (pattern, what) in Flags)
        {
            if (pattern.IsMatch(text))
            {
                warnings.Add($"The brief or done criteria mention {what}. Nothing in an approval authorises that.");
            }
        }

        return warnings;
    }

    private static readonly (Regex Pattern, string What)[] Flags =
    [
        (Branch(), "a branch or worktree"),
        (JiraCreation(), "raising a Jira item"),
        (Push(), "a push"),
        (PullRequest(), "a pull request"),
    ];

    private static string BriefText(string brief)
    {
        try
        {
            return new FileInfo(brief).Length <= LongestBriefRead ? File.ReadAllText(brief) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(branch|branches|worktree|worktrees)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Branch();

    [GeneratedRegex(@"\b(raise|create|open|file)\s+(a\s+|an\s+|new\s+)*(jira|ticket|issue|story|bug|epic|task)\b", RegexOptions.IgnoreCase)]
    private static partial Regex JiraCreation();

    [GeneratedRegex(@"\b(git\s+push|push\s+(it|this|the|to)|force-push)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Push();

    [GeneratedRegex(@"\b((?i:pull\s+requests?)|PRs?)\b")]
    private static partial Regex PullRequest();
}
