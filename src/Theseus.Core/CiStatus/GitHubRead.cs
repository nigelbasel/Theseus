namespace Theseus.Core.CiStatus;

/// <summary>A read of one repository, and the budget GitHub reported while making it.</summary>
/// <param name="Reading">What was found.</param>
/// <param name="Budget">The calls left, or null when no answer reported it.</param>
public sealed record GitHubRead(RepoReading Reading, GitHubBudget? Budget);
