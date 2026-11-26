namespace Theseus.Core.Projects;

/// <summary>
/// What Stop the project does, said once before it is done (project-view-as-tabs AC 10): which cards are asked to hand
/// over and stop, the duties that pause, the overnight queue, and the lock holds and queue places released once each card
/// has stopped. A test run keeps its lock until its run ends, and nothing is closed.
/// </summary>
public sealed class ProjectStopConfirmation
{
    private ProjectStopConfirmation(string title, string question)
    {
        Title = title;
        Question = question;
    }

    /// <summary>The project's title.</summary>
    public string Title { get; }

    /// <summary>The confirmation's title, a question (W23).</summary>
    public string Caption => $"Stop {Title}?";

    /// <summary>The confirmation's action button, with its access key (A3).</summary>
    public string Action => "_Stop project";

    /// <summary>What the stop does, in a few sentences.</summary>
    public string Question { get; }

    /// <summary>The stop of a project.</summary>
    /// <param name="title">The project's title.</param>
    /// <param name="cards">Its cards.</param>
    /// <param name="duties">How many of its duties are live and pause.</param>
    /// <param name="queueOn">Whether its overnight queue is on.</param>
    /// <param name="holds">How many session holds its cards have, released once each has stopped.</param>
    /// <param name="places">How many places in lock queues its cards have, given up once each has stopped.</param>
    /// <param name="runs">How many test runs of its cards hold a lock, which they keep until they end.</param>
    public static ProjectStopConfirmation For(string title, IReadOnlyList<ProjectCloseCard> cards, int duties, bool queueOn, int holds,
        int places, int runs)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(cards);

        var running = cards.Where(card => card.HasSession).ToList();
        var lines = new List<string>
        {
            running.Count switch
            {
                0 => $"No card of {title} is running.",
                1 => $"1 card is running, {running[0].Title}: it is asked to finish its turn, write its handover and stop.",
                _ => $"{running.Count} cards are running, {string.Join(", ", running.Select(card => card.Title))}: each is asked to finish its turn, write its handover and stop.",
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

        lines.Add((holds, places) switch
        {
            (0, 0) when runs == 0 => "Its cards hold no lock.",
            (0, 0) => "Its cards hold no other lock.",
            _ => $"{Count(holds, "lock hold")}{(holds > 0 && places > 0 ? " and " : string.Empty)}{Places(places)} {(holds + places == 1 ? "is" : "are")} released once each card has stopped.",
        });

        if (runs > 0)
        {
            lines.Add(runs == 1 ? "1 test run keeps its lock until it ends." : $"{runs} test runs keep their locks until they end.");
        }

        lines.Add("Nothing is closed: Start the project brings back the cards that were running, its duties and its queue.");

        return new ProjectStopConfirmation(title, string.Join(" ", lines));
    }

    private static string Count(int count, string noun) => count switch
    {
        0 => string.Empty,
        1 => $"1 {noun}",
        _ => $"{count} {noun}s",
    };

    private static string Places(int count) => count switch
    {
        0 => string.Empty,
        1 => "1 place in a lock queue",
        _ => $"{count} places in lock queues",
    };
}
