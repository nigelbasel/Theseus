using System.Globalization;
using System.Text.Json.Nodes;
using Theseus.Core.Domain;
using Theseus.Core.Duties;

namespace Theseus.Core.Mcp;

/// <summary>
/// set_duty, duties, pause_duty and end_duty: a session asks Theseus to give a card an instruction on a cadence instead of
/// running a loop in its own tab (Nigel, 2026-11-24). A session may act on its own card and on the cards start_workstream
/// lets it act on; a duty the owner set is his alone to change.
/// </summary>
public sealed class DutyTools
{
    /// <summary>set_duty.</summary>
    public const string SetDuty = "set_duty";

    /// <summary>duties.</summary>
    public const string ListDuties = "duties";

    /// <summary>pause_duty.</summary>
    public const string PauseDuty = "pause_duty";

    /// <summary>end_duty.</summary>
    public const string EndDuty = "end_duty";

    /// <summary>Every tool this class answers.</summary>
    public static readonly IReadOnlyList<string> Names = [SetDuty, ListDuties, PauseDuty, EndDuty];

    private readonly DutyBook _book;
    private readonly TimeZoneInfo _zone;
    private readonly Action<string, string> _record;

    /// <summary>Creates the tools over the board's duties.</summary>
    /// <param name="book">The duties.</param>
    /// <param name="zone">The local time zone, for the times it shows.</param>
    /// <param name="record">Keeps a change in the card's History and the log: the card's key, then what was done.</param>
    public DutyTools(DutyBook book, TimeZoneInfo? zone = null, Action<string, string>? record = null)
    {
        _book = book;
        _zone = zone ?? TimeZoneInfo.Local;
        _record = record ?? ((_, _) => { });
    }

    /// <summary>The tools' definitions for tools/list.</summary>
    public static IReadOnlyList<JsonObject> Definitions() =>
    [
        Tool(SetDuty,
            "Give a card an instruction again and again on a cadence, instead of running a loop in your own tab: Theseus keeps it across restarts, hands it in when the card is idle, starts the card when it is stopped, waits while it works or asks the user something, and skips it while it is paused. Each run is told what changed since the last. Set it on your own card or on any card workstreams lists. Setting it again by name changes it. Give every or at.",
            new JsonObject
            {
                ["card"] = Property("Optional: the card's key or title; your own card when left out."),
                ["name"] = Property("A short name, unique on the card, such as Re-triage."),
                ["instruction"] = Property("What the card is told each run."),
                ["every"] = Property("An interval, at least 5 minutes, such as 20m or 2h."),
                ["at"] = Property("Instead of every: times of day, such as 07:00, 13:00."),
                ["days"] = Property("Optional, with at: weekdays, such as Mon-Fri; every day when left out."),
                ["until"] = Property("Optional: never (the default), a date yyyy-MM-dd, or criterion: <its text>, ending it once that criterion of its objective is ticked."),
                ["through_quiet_hours"] = new JsonObject { ["type"] = "boolean", ["description"] = "Optional: true to run through the user's quiet hours." },
                ["daily_cap"] = new JsonObject { ["type"] = "integer", ["description"] = "Optional: the most runs in a day; the board's default when left out." },
            },
            "name", "instruction"),
        Tool(ListDuties,
            "The duties on your own card and on the cards workstreams lists, each with its cadence, next run, last run and state. Read-only.",
            new JsonObject { ["card"] = Property("Optional: one card's key or title.") }),
        Tool(PauseDuty,
            "Pause a duty set by you or another session; it stays on the card and does not run until set_duty is called again for it or the user resumes it.",
            new JsonObject
            {
                ["card"] = Property("Optional: the card's key or title; your own card when left out."),
                ["name"] = Property("The duty's name."),
                ["why"] = Property("Optional: why, shown on the card."),
            },
            "name"),
        Tool(EndDuty,
            "End a duty for good, such as once the work it watches is finished. A duty the user set is theirs to end.",
            new JsonObject
            {
                ["card"] = Property("Optional: the card's key or title; your own card when left out."),
                ["name"] = Property("The duty's name."),
                ["why"] = Property("Optional: why, kept in its History."),
            },
            "name"),
    ];

    /// <summary>Answers one call.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="session">The calling session.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="board">The board as of its last refresh.</param>
    /// <param name="allowed">The cards it may act on: its own and those start_workstream lets it.</param>
    public (string Text, bool IsError) Call(string tool, McpSession session, JsonObject arguments, BoardSnapshot board,
        IReadOnlyList<BoardSnapshotCard> allowed)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(allowed);

        if (tool == ListDuties)
        {
            return List(arguments, board, allowed);
        }

        if (Card(arguments, session, board, allowed) is not { } card)
        {
            return (Named(arguments) is { } named && board.Card(named) is null && !board.Cards.Any(each => SameTitle(each, named))
                ? $"No card on the board is called {named}."
                : $"{Named(arguments)} is not a card you may set duties on: your own card, and those workstreams lists.", true);
        }

        var name = Text(arguments["name"]);

        if (name is null)
        {
            return ("Name the duty.", true);
        }

        return tool switch
        {
            SetDuty => Set(session, card, name, arguments),
            PauseDuty or EndDuty => Change(tool, session, card, name, Text(arguments["why"])),
            _ => ($"Unknown tool: {tool}", true),
        };
    }

    private (string, bool) Set(McpSession session, BoardSnapshotCard card, string name, JsonObject arguments)
    {
        var every = Text(arguments["every"]);
        var at = Text(arguments["at"]);
        DutyCadence cadence;

        if (every is not null && at is null)
        {
            if (DutyText.Interval(every) is not { } interval)
            {
                return ($"\"{every}\" is not an interval: give one such as 20m or 2h.", true);
            }

            cadence = DutyCadence.Every(interval);
        }
        else if (at is not null && every is null)
        {
            if (DutyText.Times(at) is not { } times)
            {
                return ($"\"{at}\" is not a list of times: give them such as 07:00, 13:00.", true);
            }

            if (DutyText.Days(Text(arguments["days"])) is not { } days)
            {
                return ($"\"{Text(arguments["days"])}\" are not weekdays: give them such as Mon-Fri or Mon, Wed.", true);
            }

            cadence = DutyCadence.At(times, days);
        }
        else
        {
            return ("Give every or at, not both.", true);
        }

        if (DutyText.End(Text(arguments["until"])) is not { } end)
        {
            return ($"\"{Text(arguments["until"])}\" is not an end: give never, a date yyyy-MM-dd, or criterion: <its text>.", true);
        }

        if (end.Kind == DutyEndKind.OnCriterion && card.Workstream.Project is null)
        {
            return ("Only a card in a project can end on a criterion.", true);
        }

        var cap = arguments["daily_cap"] is JsonValue capValue && capValue.TryGetValue<int>(out var number) ? number : (int?)null;
        var through = arguments["through_quiet_hours"] is JsonValue quiet && quiet.TryGetValue<bool>(out var flag) && flag;
        var existing = _book.Find(card.Key, name);
        var resumes = existing?.PausedBy is { } by && !string.Equals(by, Duty.Owner, StringComparison.Ordinal)
                                                   && !string.Equals(by, Duty.Theseus, StringComparison.Ordinal);
        var (duty, updated, refusal) = _book.Set(card.Key, name, Text(arguments["instruction"]) ?? string.Empty, cadence, end,
            session.WorkstreamKey, cap, through);

        if (refusal is not null)
        {
            return (refusal, true);
        }

        if (resumes)
        {
            duty = _book.Resume(duty!.Id) ?? duty;
        }

        var verb = updated ? "Changed" : "Set";

        _record(card.Key, $"{verb} duty \"{duty!.Name}\" ({duty.Cadence.Describe()}) by {session.WorkstreamTitle}");

        return ($"{verb} duty \"{duty.Name}\" on {card.Workstream.Title}: {duty.Cadence.Describe()}, {duty.End.Describe()}; next run {Time(duty.State.NextDueUtc)}."
                + (existing?.PausedBy is Duty.Owner or Duty.Theseus ? $" It stays paused ({existing.PauseReason ?? "by the user"}): only the user resumes it." : string.Empty), false);
    }

    private (string, bool) Change(string tool, McpSession session, BoardSnapshotCard card, string name, string? why)
    {
        if (_book.Find(card.Key, name) is not { } duty)
        {
            return ($"{card.Workstream.Title} has no duty called {name}.", true);
        }

        if (duty.IsOwners)
        {
            return ($"The duty \"{duty.Name}\" was set by the user: only they {(tool == EndDuty ? "end" : "pause")} it.", true);
        }

        if (tool == EndDuty)
        {
            _book.End(duty.Id, why ?? $"ended by {session.WorkstreamTitle}");
            _record(card.Key, $"Duty \"{duty.Name}\" ended by {session.WorkstreamTitle}{Why(why)}");

            return ($"Ended duty \"{duty.Name}\" on {card.Workstream.Title}.", false);
        }

        _book.Pause(duty.Id, session.WorkstreamKey, why);
        _record(card.Key, $"Duty \"{duty.Name}\" paused by {session.WorkstreamTitle}{Why(why)}");

        return ($"Paused duty \"{duty.Name}\" on {card.Workstream.Title}. set_duty with its name resumes it.", false);
    }

    private (string, bool) List(JsonObject arguments, BoardSnapshot board, IReadOnlyList<BoardSnapshotCard> allowed)
    {
        var named = Named(arguments);
        var cards = named is null
            ? allowed
            : [.. allowed.Where(card => string.Equals(card.Key, named, StringComparison.OrdinalIgnoreCase) || SameTitle(card, named))];
        var lines = cards.SelectMany(card => _book.For(card.Key).Select(duty => Line(card, duty))).ToList();

        return (lines.Count == 0 ? "No duties on the cards you may act on." : string.Join(Environment.NewLine, lines), false);
    }

    private string Line(BoardSnapshotCard card, Duty duty)
    {
        var state = duty.IsPaused ? $"paused: {duty.PauseReason ?? (duty.PausedBy == Duty.Owner ? "by the user" : duty.PausedBy)}"
            : duty.State.Waiting is { } waiting ? $"waiting: {waiting}"
            : $"next {Time(duty.State.NextDueUtc)}";
        var last = duty.State.LastRunUtc is { } at ? $"; last {Time(at)}, {DutyPrompt.Word(duty.State.LastOutcome)}" : string.Empty;
        var by = duty.IsOwners ? "the user" : duty.SetBy;

        return $"{card.Workstream.Title} ({card.Key}): \"{duty.Name}\", {duty.Cadence.Describe()}, {state}{last}; set by {by}. {duty.FirstLine()}";
    }

    private static BoardSnapshotCard? Card(JsonObject arguments, McpSession session, BoardSnapshot board, IReadOnlyList<BoardSnapshotCard> allowed)
    {
        var named = Named(arguments) ?? session.WorkstreamKey;
        var byKey = allowed.FirstOrDefault(card => string.Equals(card.Key, named, StringComparison.OrdinalIgnoreCase));
        var byTitle = allowed.Where(card => SameTitle(card, named)).ToList();

        return byKey ?? (byTitle.Count == 1 ? byTitle[0] : null);
    }

    private static bool SameTitle(BoardSnapshotCard card, string named) =>
        string.Equals(card.Workstream.Title, named, StringComparison.OrdinalIgnoreCase);

    private static string? Named(JsonObject arguments) => Text(arguments["card"]);

    private string Time(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, _zone).ToString("ddd HH:mm", CultureInfo.InvariantCulture);

    private static string Why(string? why) => string.IsNullOrWhiteSpace(why) ? string.Empty : $": {why.Trim()}";

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    private static JsonObject Tool(string name, string description, JsonObject properties, params string[] required) => new()
    {
        ["name"] = name,
        ["description"] = description,
        ["inputSchema"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray([.. required.Select(each => (JsonNode)each)]),
        },
    };

    private static JsonObject Property(string description) => new() { ["type"] = "string", ["description"] = description };
}
