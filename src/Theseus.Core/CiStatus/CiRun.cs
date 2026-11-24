namespace Theseus.Core.CiStatus;

/// <summary>One GitHub Actions workflow run, as the repository's run list gives it.</summary>
/// <param name="Branch">The branch it ran on.</param>
/// <param name="Status">"queued", "in_progress", "completed" and GitHub's other waiting states.</param>
/// <param name="Conclusion">"success", "failure", "cancelled" and the rest, once completed; null before.</param>
/// <param name="Url">Its page.</param>
/// <param name="CreatedAt">When it was queued, which orders runs newest first.</param>
/// <param name="StartedAt">When it started running, if it has.</param>
/// <param name="UpdatedAt">When it last changed, which for a completed run is when it finished.</param>
public sealed record CiRun(string Branch, string Status, string? Conclusion, string Url, DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt, DateTimeOffset UpdatedAt);
