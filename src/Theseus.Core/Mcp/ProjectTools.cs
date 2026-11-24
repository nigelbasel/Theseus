using System.Text.Json.Nodes;
using Theseus.Core.Diagnostics;
using Theseus.Core.Projects;

namespace Theseus.Core.Mcp;

/// <summary>
/// The tools a project's sessions use to tell Theseus where they are and to write project.md's structured lines (1.1f
/// E24, B9; brief, PR 2: Theseus is the only writer of what the reader parses, so sessions never hand-edit it). Each
/// edit goes through <see cref="ProjectFileEditor"/> and <see cref="ProjectFileWriter"/>: validated, refused with its
/// reason when the file changed since Theseus read it or would not read afterwards, and logged.
/// </summary>
public sealed class ProjectTools
{
    /// <summary>The longest <c>doing</c> now_at takes (E24).</summary>
    public const int DoingLength = 80;

    private const string SkippedPrefix = "skipped:";

    private readonly Func<string, ProjectMember?> _memberOf;
    private readonly Func<string, ProjectFileReading?> _readingOf;
    private readonly StageReportBook _reports;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeZoneInfo _zone;
    private readonly Func<string, Func<string, string>, string> _write;
    private readonly Lock _writing = new();

    /// <summary>Creates the tools.</summary>
    /// <param name="memberOf">A card's project and current piece, by key, or null when no project names it.</param>
    /// <param name="readingOf">A project as last read, by name, or null.</param>
    /// <param name="reports">Where now_at's reports and skips are kept.</param>
    /// <param name="clock">The time now.</param>
    /// <param name="zone">The zone a date is written in; this machine's when null.</param>
    /// <param name="write">
    /// Applies an edit to a project's file, by name, and returns the text written, throwing
    /// <see cref="ProjectEditRefusedException"/> when it is refused. The app passes the board's own edit path, so tools and
    /// the owner's edits are one at a time and each is read back before the next (review of PR 2, finding 2). When null,
    /// the file is written directly under this object's lock.
    /// </param>
    public ProjectTools(Func<string, ProjectMember?> memberOf, Func<string, ProjectFileReading?> readingOf, StageReportBook reports,
        Func<DateTimeOffset> clock, TimeZoneInfo? zone = null, Func<string, Func<string, string>, string>? write = null)
    {
        _write = write ?? WriteDirectly;
        _memberOf = memberOf;
        _readingOf = readingOf;
        _reports = reports;
        _clock = clock;
        _zone = zone ?? TimeZoneInfo.Local;
    }

    /// <summary>The tools' names.</summary>
    public static IReadOnlyList<string> Names { get; } =
        ["now_at", "record_round", "tick_criterion", "add_objective", "record_decision", "set_waiting"];

    /// <summary>Runs one of the tools for a session.</summary>
    public ProjectToolOutcome Call(string name, McpSession session, JsonObject arguments)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(arguments);

        if (_memberOf(session.WorkstreamKey) is not { } member || _readingOf(member.Project) is not { IsReadable: true } reading)
        {
            return ProjectToolOutcome.Refused("This workstream is not in a project that reads, so there is nothing to record against.");
        }

        try
        {
            return name switch
            {
                "now_at" => NowAt(session, member, reading, arguments),
                "record_round" => RecordRound(session, reading, arguments),
                "tick_criterion" => TickCriterion(session, reading, arguments),
                "add_objective" => AddObjective(session, reading, arguments),
                "record_decision" => RecordDecision(session, reading, arguments),
                "set_waiting" => SetWaiting(session, reading, arguments),
                _ => ProjectToolOutcome.Refused($"Unknown tool: {name}"),
            };
        }
        catch (ProjectEditRefusedException refused)
        {
            TheseusLog.Warning("{Tool} by {Card} in {Project} refused: {Reason}", name, session.WorkstreamKey, member.Project, refused.Message);

            return ProjectToolOutcome.Refused($"Refused: {refused.Message}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "{Tool} by {Card} in {Project} could not write", name, session.WorkstreamKey, member.Project);

            return ProjectToolOutcome.Refused($"Refused: the file could not be written ({exception.Message}).");
        }
    }

    private ProjectToolOutcome NowAt(McpSession session, ProjectMember member, ProjectFileReading reading, JsonObject arguments)
    {
        var stageWord = Text(arguments, "stage");
        var doing = Text(arguments, "doing");

        if (stageWord is null || doing is null)
        {
            return ProjectToolOutcome.Refused("now_at needs a stage and what you are doing.");
        }

        var file = reading.File!;
        var objective = file.ObjectiveOf(member.Slug);
        var artefacts = reading.Artefacts.GetValueOrDefault(member.Slug) ?? WorkstreamArtefacts.NotStarted(member.Slug);
        var pipeline = objective is null ? null : StageCalculator.PipelineFor(objective.Pipelines, artefacts.Files);
        var stages = pipeline is { } chosen ? StageCalculator.StagesOf(chosen) : [];
        PipelineStage? stage = null;

        if (stages.Count == 0)
        {
            if (!string.Equals(stageWord.Trim(), FreeformStageNode.WorkName, StringComparison.OrdinalIgnoreCase))
            {
                return ProjectToolOutcome.Refused($"{member.Slug} is freeform: its one stage is Work.");
            }
        }
        else if (SkippedStages.StageNamed(stageWord) is { } named && stages.Contains(named))
        {
            stage = named;
        }
        else
        {
            return ProjectToolOutcome.Refused(
                $"{stageWord} is not a stage of {member.Slug}. Its stages are {string.Join(", ", stages.Select(each => Name(pipeline!.Value, each)))}.");
        }

        if (doing.StartsWith(SkippedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var reason = doing[SkippedPrefix.Length..].Trim();

            if (stage is not { } skipped || reason.Length == 0 || reason.Length > 200 || reason.Contains('\n'))
            {
                return ProjectToolOutcome.Refused("A skip names a stage of the pipeline and says why in one line: now_at(stage, \"skipped: <reason>\").");
            }

            // Only the owner passes the spec gate and only the independent check passes Verify, so neither is skipped.
            if (skipped is PipelineStage.Spec or PipelineStage.Verify)
            {
                return ProjectToolOutcome.Refused(
                    $"{stageWord} cannot be skipped: only the owner's acceptance passes the spec gate, and only the independent check passes Verify.");
            }

            _reports.Skip(member.Project, member.Slug, skipped, reason, _clock());
            TheseusLog.Information("now_at: {Card} skipped {Stage} of {Piece} in {Project}: {Reason}", session.WorkstreamKey, skipped,
                member.Slug, member.Project, reason);

            return new ProjectToolOutcome($"Recorded: {stageWord} of {member.Slug} was skipped ({reason}).");
        }

        if (doing.Length >= DoingLength || doing.Contains('\n'))
        {
            return ProjectToolOutcome.Refused($"Say what you are doing in one line of under {DoingLength} characters.");
        }

        _reports.Report(session.WorkstreamKey, new SessionStageReport(stage, doing, _clock()));
        TheseusLog.Information("now_at: {Card} at {Stage} of {Piece}: {Doing}", session.WorkstreamKey, stageWord, member.Slug, doing);

        return new ProjectToolOutcome($"Recorded: {member.Slug} is at {stageWord}, {doing}. The project view shows it until your next now_at.");
    }

    private ProjectToolOutcome RecordRound(McpSession session, ProjectFileReading reading, JsonObject arguments)
    {
        var name = Text(arguments, "name");
        var why = Text(arguments, "why");
        var objectives = Strings(arguments, "objectives");

        if (name is null || why is null || objectives.Count == 0)
        {
            return ProjectToolOutcome.Refused("record_round needs the round's name, why the requirements changed, and the objectives it adds or reopens.");
        }

        var by = Text(arguments, "by") ?? "Nigel";
        var number = 0;
        Write(reading, text =>
        {
            var (edited, round) = ProjectFileEditor.AddRound(text, name, why, by, Today(), objectives);
            number = round;

            return edited;
        });
        Log(session, reading, $"round {number}, {name}, for objectives {string.Join(", ", objectives)}");

        return new ProjectToolOutcome($"Recorded round {number}, {name}, with objectives {string.Join(", ", objectives)} in it.");
    }

    private ProjectToolOutcome TickCriterion(McpSession session, ProjectFileReading reading, JsonObject arguments)
    {
        if (Criterion(reading, arguments) is not { } at)
        {
            return ProjectToolOutcome.Refused("tick_criterion needs the objective's number, the criterion's number from 1 and its text as the file has it.");
        }

        var ticked = arguments["ticked"] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : true;
        var evidence = Text(arguments, "evidence");

        if (ticked && evidence is null)
        {
            return ProjectToolOutcome.Refused("A tick needs its evidence: a one-line pointer to what shows the criterion is met.");
        }

        Write(reading, text =>
        {
            var edited = ProjectFileEditor.SetCriterion(text, at.Objective, at.Index, at.Text, ticked);

            // An unticked criterion keeps no evidence: the pointer said it was met.
            return ProjectFileEditor.SetCriterionNote(edited, at.Objective, at.Index, at.Text, ProjectCriterion.EvidenceLabel,
                ticked ? evidence : null);
        });
        Log(session, reading, $"{(ticked ? "ticked" : "unticked")} criterion {at.Index + 1} of objective {at.Objective}");

        return new ProjectToolOutcome($"{(ticked ? "Ticked" : "Unticked")} criterion {at.Index + 1} of objective {at.Objective}.");
    }

    private ProjectToolOutcome AddObjective(McpSession session, ProjectFileReading reading, JsonObject arguments)
    {
        var title = Text(arguments, "title");
        var criteria = Strings(arguments, "criteria");
        var waiting = Strings(arguments, "waiting");

        if (arguments["phase"] is not JsonValue phaseValue || !phaseValue.TryGetValue<int>(out var phase) || title is null)
        {
            return ProjectToolOutcome.Refused("add_objective needs the phase's number and the objective's title.");
        }

        PipelineKind? pipeline = Text(arguments, "pipeline")?.ToLowerInvariant() switch
        {
            null or "[to be chosen]" => null,
            "feature" => PipelineKind.Feature,
            "bug" => PipelineKind.Bug,
            "freeform" => PipelineKind.Freeform,
            var other => throw new ProjectEditRefusedException($"pipeline '{other}' is not feature, bug or freeform"),
        };

        if (waiting.Count > criteria.Count)
        {
            return ProjectToolOutcome.Refused("waiting has one entry per criterion, in the same order; it has more than there are criteria.");
        }

        var notes = waiting.Select(Waiting).ToList();
        var number = string.Empty;
        Write(reading, text =>
        {
            var (edited, added) = ProjectFileEditor.AddObjective(text, phase, title, criteria, pipeline);
            number = added;

            for (var index = 0; index < notes.Count; index++)
            {
                if (notes[index] is { } note)
                {
                    edited = ProjectFileEditor.SetCriterionNote(edited, added, index, criteria[index].Trim(), CriterionWaiting.Label,
                        note.Line[CriterionWaiting.Label.Length..].Trim());
                }
            }

            return edited;
        });
        Log(session, reading, $"objective {number}, {title}");

        return new ProjectToolOutcome($"Added objective {number}: {title}.");
    }

    private ProjectToolOutcome RecordDecision(McpSession session, ProjectFileReading reading, JsonObject arguments)
    {
        var decision = Text(arguments, "decision");
        var by = Text(arguments, "by");

        if (decision is null || by is null)
        {
            return ProjectToolOutcome.Refused("record_decision needs the decision and who made it.");
        }

        Write(reading, text => ProjectFileEditor.AddDecision(text, Today(), decision, by));
        Log(session, reading, $"decision by {by}: {decision}");

        return new ProjectToolOutcome($"Recorded the decision, dated {Today():yyyy-MM-dd}.");
    }

    private ProjectToolOutcome SetWaiting(McpSession session, ProjectFileReading reading, JsonObject arguments)
    {
        if (Criterion(reading, arguments) is not { } at)
        {
            return ProjectToolOutcome.Refused("set_waiting needs the objective's number, the criterion's number from 1 and its text as the file has it.");
        }

        // The owner is a value of its own; anyone else is named, and the line says "on <who>:", so a wait on someone else is
        // never counted as his (git-22 for Nigel, 2026-11-24).
        var named = Text(arguments, "who")?.Trim();
        var who = named?.ToLowerInvariant() is "owner" or "work" or "none" ? named.ToLowerInvariant() : named;
        var what = Text(arguments, "what");

        if (!WaitingAsk.IsWritableWho(who) || (who != "none" && what is null))
        {
            return ProjectToolOutcome.Refused($"who is owner, work, none, or the name of whoever else it waits on ({WaitingAsk.LongestWho} characters at most, no colon or semicolon); all but none need what it waits for.");
        }

        if (who != "none" && reading.File!.Objectives.FirstOrDefault(objective => objective.Number == at.Objective)?.Criteria
                .ElementAtOrDefault(at.Index) is { IsTicked: true })
        {
            return ProjectToolOutcome.Refused("That criterion is ticked, so it waits on no one.");
        }

        Write(reading, text => ProjectFileEditor.SetCriterionNote(text, at.Objective, at.Index, at.Text, CriterionWaiting.Label,
            who switch { "none" => null, "owner" or "work" => $"{who}: {what}", _ => $"on {who}: {what}" }));
        Log(session, reading, $"criterion {at.Index + 1} of objective {at.Objective} waits on {who}");

        return new ProjectToolOutcome(who == "none"
            ? $"Criterion {at.Index + 1} of objective {at.Objective} no longer says what it waits on."
            : $"Criterion {at.Index + 1} of objective {at.Objective} now waits on {who}: {what}.");
    }

    private static CriterionWaiting? Waiting(string entry) =>
        entry.Trim().Length == 0 ? null : CriterionWaiting.Parse($"{CriterionWaiting.Label} {entry.Trim()}")
                                          ?? throw new ProjectEditRefusedException($"waiting entry '{entry}' is not 'owner: <what>' or 'work: <what>'");

    private static (string Objective, int Index, string Text)? Criterion(ProjectFileReading reading, JsonObject arguments)
    {
        var objective = Text(arguments, "objective");
        var text = Text(arguments, "text");

        if (objective is null || text is null || arguments["criterion"] is not JsonValue value || !value.TryGetValue<int>(out var number))
        {
            return null;
        }

        return (objective, number - 1, text);
    }

    private void Write(ProjectFileReading reading, Func<string, string> edit) => _write(reading.Name, edit);

    private string WriteDirectly(string project, Func<string, string> edit)
    {
        lock (_writing)
        {
            return ProjectFileWriter.Apply(_readingOf(project) ?? throw new ProjectEditRefusedException($"{project} does not read"), edit);
        }
    }

    private static void Log(McpSession session, ProjectFileReading reading, string what) =>
        TheseusLog.Information("{Card} wrote {Project}'s project.md through a tool: {What}", session.WorkstreamKey, reading.Name, what);

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock(), _zone).DateTime);

    private static string Name(PipelineKind pipeline, PipelineStage stage) => (pipeline, stage) switch
    {
        (PipelineKind.Bug, PipelineStage.Research) => "Reproduce",
        (PipelineKind.Bug, PipelineStage.Spec) => "Rule",
        (PipelineKind.Bug, PipelineStage.Implement) => "Fix",
        (_, PipelineStage.Implement) => "Build",
        (_, PipelineStage.Compound) => "Lessons",
        _ => stage.ToString(),
    };

    private static string? Text(JsonObject arguments, string name) =>
        arguments[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Trim().Length > 0 ? text.Trim() : null;

    private static List<string> Strings(JsonObject arguments, string name) =>
        arguments[name] is JsonArray array
            ? [.. array.Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty)]
            : [];
}
