namespace Theseus.Core.Reporting;

/// <summary>
/// One workstream's day.
/// </summary>
/// <param name="WorkstreamKey">The workstream, by key.</param>
/// <param name="Title">What to call it.</param>
/// <param name="Area">The row of the board it sits in, which is how the report is grouped.</param>
/// <param name="Worked">
/// Time spent within the day, not the length of the sessions. A session running from last night
/// until this afternoon belongs to both days, and giving either of them the whole of it would be
/// wrong twice over.
/// </param>
/// <param name="Sessions">How many sessions contributed.</param>
/// <param name="StillRunning">Whether one of them is still going, so the figure is not final.</param>
/// <param name="JiraKeys">
/// Every issue on the card. Listed rather than apportioned: a workstream spanning sixteen issues
/// cannot be split between them without somebody saying how, and a guessed split would be a
/// confident wrong number in a record meant to be permanent.
/// </param>
/// <param name="HandedOverToday">Whether its handover document was written today.</param>
/// <param name="UnmeasuredSessions">
/// Sessions that touched the day but whose length cannot be known, because nobody observed them
/// ending and they are not running now. Counted rather than guessed at: a run log holding weeks of
/// unended runs would otherwise credit today with all of them.
/// </param>
public sealed record DayReportEntry(
    string WorkstreamKey,
    string Title,
    string Area,
    TimeSpan Worked,
    int Sessions,
    bool StillRunning,
    IReadOnlyList<string> JiraKeys,
    bool HandedOverToday,
    int UnmeasuredSessions = 0);
