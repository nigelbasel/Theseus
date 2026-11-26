namespace Theseus.Core.Projects;

/// <summary>
/// What closing or archiving a project does, said once before it is done (archive-a-project, brief item 2): how many of
/// its cards are running and are asked to hand over and stop, how many duties pause, whether its overnight queue goes
/// off, and, for an archive, that its cards are archived while its records stay where they are (Nigel, 2026-11-26).
/// </summary>
public sealed class ProjectClose
{
    private ProjectClose(ProjectClosureKind kind, string title, IReadOnlyList<ProjectCloseCard> running, string question)
    {
        Kind = kind;
        Title = title;
        Running = running;
        Question = question;
    }

    /// <summary>Closed or archived.</summary>
    public ProjectClosureKind Kind { get; }

    /// <summary>The project's title.</summary>
    public string Title { get; }

    /// <summary>Its cards with a session open, asked to hand over and stop.</summary>
    public IReadOnlyList<ProjectCloseCard> Running { get; }

    /// <summary>The confirmation's title, a question (W23).</summary>
    public string Caption => Kind == ProjectClosureKind.Archived ? $"Archive {Title}?" : $"Close {Title}?";

    /// <summary>The confirmation's action button, a verb that says what it does, with its access key (A3).</summary>
    public string Action => Kind == ProjectClosureKind.Archived ? "_Archive project" : "_Close project";

    /// <summary>What the close does, in a few sentences.</summary>
    public string Question { get; }

    /// <summary>The close of a project, from its cards, its live duties and whether its overnight queue is on.</summary>
    public static ProjectClose For(ProjectClosureKind kind, string title, IReadOnlyList<ProjectCloseCard> cards, int duties, bool queueOn)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(cards);

        var running = cards.Where(card => card.HasSession).ToList();
        var lines = new List<string>
        {
            running.Count switch
            {
                0 => $"No card of {title} is running.",
                1 => $"1 card is running, {running[0].Title}: it is asked to write its handover and stop.",
                _ => $"{running.Count} cards are running, {string.Join(", ", running.Select(card => card.Title))}: each is asked to write its handover and stop.",
            },
        };

        if (duties > 0)
        {
            lines.Add(duties == 1 ? "1 duty pauses." : $"{duties} duties pause.");
        }

        if (queueOn)
        {
            lines.Add("Its overnight queue goes off.");
        }

        if (kind == ProjectClosureKind.Archived)
        {
            var archived = cards.Count(card => !card.IsArchived);

            if (archived > 0)
            {
                lines.Add(archived == 1
                    ? "Its card is archived once its session has ended."
                    : $"Its {archived} cards are archived, each once its session has ended.");
            }

            lines.Add("project.md and its records stay where they are.");
        }

        lines.Add("It leaves the board's projects and the waits count. Reopen, under Archived on the board, puts it back as it was.");

        return new ProjectClose(kind, title, running, string.Join(" ", lines));
    }
}
