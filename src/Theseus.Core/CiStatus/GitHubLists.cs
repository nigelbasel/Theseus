using System.Globalization;
using System.Text.Json;

namespace Theseus.Core.CiStatus;

/// <summary>Reads GitHub's open pull request list and workflow run list, as the REST API returns them.</summary>
public static class GitHubLists
{
    /// <summary>The open pull requests in a list; an entry missing what the card needs is skipped.</summary>
    public static IReadOnlyList<OpenPullRequest> PullRequests(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var found = new List<OpenPullRequest>();

        foreach (var pull in document.RootElement.EnumerateArray())
        {
            if (pull.ValueKind == JsonValueKind.Object
                && pull.TryGetProperty("number", out var number) && number.ValueKind == JsonValueKind.Number
                && number.TryGetInt32(out var value)
                && Page(pull) is { } url
                && pull.TryGetProperty("head", out var head) && head.ValueKind == JsonValueKind.Object
                && Text(head, "ref") is { } branch)
            {
                var headRepository = head.TryGetProperty("repo", out var repo) && repo.ValueKind == JsonValueKind.Object
                    ? Text(repo, "full_name")
                    : null;

                found.Add(new OpenPullRequest(value, Text(pull, "title") ?? string.Empty, branch, headRepository,
                    pull.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True, url));
            }
        }

        return found;
    }

    /// <summary>The runs in a run list; an entry missing what the card needs is skipped.</summary>
    public static IReadOnlyList<CiRun> Runs(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("workflow_runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var found = new List<CiRun>();

        foreach (var run in runs.EnumerateArray())
        {
            if (run.ValueKind == JsonValueKind.Object
                && Text(run, "head_branch") is { } branch && Text(run, "status") is { } status
                && Page(run) is { } url && Time(run, "created_at") is { } created)
            {
                found.Add(new CiRun(branch, status, Text(run, "conclusion"), url, created, Time(run, "run_started_at"),
                    Time(run, "updated_at") ?? created));
            }
        }

        return found;
    }

    // A row opens its address in the browser, so only a github.com page is taken (review nit).
    private static string? Page(JsonElement element) =>
        Text(element, "html_url") is { } url && url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ? url : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        Text(element, name) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time
            : null;
}
