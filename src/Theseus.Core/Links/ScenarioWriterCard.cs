using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>
/// The card Write scenarios… creates when a project has no holdout scenarios yet (1.1e AC 6c): a session allowed to write
/// in the project's holdout folder, which it works in, with the project's home in reach to read its specs. It writes
/// scenarios only, never code, and starts only when the owner starts it.
/// </summary>
/// <param name="Title">Its title.</param>
/// <param name="WorkingDirectory">The holdout folder.</param>
/// <param name="AddDirectories">The project's home, to read its specs and objectives.</param>
/// <param name="Prompt">Its opening prompt.</param>
public sealed record ScenarioWriterCard(string Title, string WorkingDirectory, IReadOnlyList<string> AddDirectories, string Prompt)
{
    /// <summary>The role every scenario-writing card has.</summary>
    public const WorkstreamRole Role = WorkstreamRole.ScenarioWriter;

    /// <summary>The scenario writer for a project.</summary>
    /// <param name="projectTitle">The project's title.</param>
    /// <param name="holdout">Its holdout folder.</param>
    /// <param name="projectHome">Its home folder, or null for a card in no project.</param>
    /// <param name="slugs">The project's pieces of work by slug, which its Checks lines name; none for a card in no project.</param>
    public static ScenarioWriterCard For(string projectTitle, string holdout, string? projectHome, IReadOnlyList<string>? slugs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(holdout);

        var read = projectHome is null
            ? "Read the specs of the work to be judged in its records."
            : $"Read the project's project.md and the specs under {Path.Combine(projectHome, "work")}.";

        return new ScenarioWriterCard($"Write holdout scenarios for {projectTitle}", holdout, projectHome is null ? [] : [projectHome],
            string.Join(Environment.NewLine,
            [
                $"You write the holdout scenarios for {projectTitle}, in {holdout}.",
                read,
                "Write each scenario as a file there: what is set up, what is done, and what must be true afterwards, in words a verifier can check against the running work.",
                "Begin each scenario with a line \"Checks: <slug>, <slug>\" naming each piece of work it checks, by its slug; Theseus makes a verifier only for work a scenario names.",
                // Scenarios written before Checks lines name no work, so no verifier is made for them (git-22, item 7c).
                "A scenario already there without a Checks line gets one: add it, naming the work it checks, and change nothing else in it.",
                .. slugs is { Count: > 0 } ? [$"The project's work, by slug: {string.Join(", ", slugs)}."] : Array.Empty<string>(),
                $"Keep the {VerifierCard.ResultsFolder} folder for verifiers' reports. Change nothing outside {holdout}: you write scenarios, never code.",
                "When they are written, say so and stop.",
            ]));
    }
}
