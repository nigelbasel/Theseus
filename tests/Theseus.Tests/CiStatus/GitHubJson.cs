using System.Text.Json;

namespace Theseus.Tests.CiStatus;

/// <summary>Answers shaped like GitHub's, as measured on 2026-11-24 (pr-ci-status research).</summary>
public static class GitHubJson
{
    public static string Pull(int number, string branch, bool draft = false, string headRepo = "owner/repo", string title = "A change") =>
        JsonSerializer.Serialize(new
        {
            number,
            state = "open",
            title,
            draft,
            html_url = $"https://github.com/owner/repo/pull/{number}",
            head = new { @ref = branch, sha = "abc", repo = new { full_name = headRepo } },
            @base = new { @ref = "main" },
            requested_reviewers = Array.Empty<object>(),
        });

    public static string Pulls(params string[] pulls) => $"[{string.Join(",", pulls)}]";

    public static string Run(long id, string branch, string status, string? conclusion, string created, string? updated = null) =>
        JsonSerializer.Serialize(new
        {
            id,
            name = "CI",
            @event = "push",
            head_branch = branch,
            status,
            conclusion,
            html_url = $"https://github.com/owner/repo/actions/runs/{id}",
            created_at = created,
            run_started_at = created,
            updated_at = updated ?? created,
        });

    public static string Runs(params string[] runs) => $"{{\"total_count\":{runs.Length},\"workflow_runs\":[{string.Join(",", runs)}]}}";
}
