using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// The lead card New project makes, so a new project has a card for Start to start (Nigel, 2026-11-24: "I just created a
/// new project ... It has not started. How do I start it"). It works in the project's folder and is told to fill in
/// what the starter left as placeholders.
/// </summary>
/// <param name="Title">Its title.</param>
/// <param name="WorkingDirectory">The project's folder.</param>
/// <param name="Prompt">Its opening prompt.</param>
public sealed record ProjectLeadCard(string Title, string WorkingDirectory, string Prompt)
{
    /// <summary>The lead of a new project.</summary>
    /// <param name="title">The project's title.</param>
    /// <param name="folder">Its folder.</param>
    public static ProjectLeadCard For(string title, string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var file = Path.Combine(folder, ProjectCatalog.ProjectFileName);

        return new ProjectLeadCard($"{title} lead", folder, string.Join(Environment.NewLine,
            $"You are the lead of the project {title}. Its project file is {file}, and your card is its first workstream row.",
            "Call events(), then read the project file. Its objective paragraph says what the project is for. Fill in what the "
            + "starter left as placeholders: the acceptance criteria, the repos, each phase's name and exit gate, and the "
            + "objectives with their criteria and pipelines. Ask the owner with the question tool where the file does not say.",
            "Then propose the workstreams the first phase needs and start them."));
    }
}
