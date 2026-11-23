using System.IO;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Clear up verifiers… (git-22, 2026-11-23): one press stops, retires and archives the verifier cards that should not be
/// kept, which by hand was open, Stop, wait, ⋯ › Archive for every card; and Undo. Its record is kept in Theseus's
/// settings, so a stop that lands after a restart is still followed by its archive, and Undo is not lost.
/// </summary>
public sealed partial class ShellViewModel
{
    private VerifierClearUp? ClearUp => _settings.Read().VerifierClearUp is { } record ? VerifierClearUp.From(record) : null;

    /// <summary>
    /// The cards of a project Theseus made that should not be kept: its builds' wrong verifiers and its scenario writers aimed
    /// at another folder than its holdout of today (git-22, item 7a), leaving out those a clear-up already takes away.
    /// </summary>
    private IReadOnlyList<WrongVerifier> WrongVerifiersOf(string project)
    {
        var pending = ClearUp;
        var writers = Board.Cards
            .Where(card => card.Workstream.Role == WorkstreamRole.ScenarioWriter && pending?.Covers(card.Key) != true)
            .Select(card => new WriterRecord(card.Key, card.Title, WriterProject(card.Workstream), card.Workstream.WorkingDirectory, card.IsLive,
                card.Workstream.IsArchived))
            .Where(writer => string.Equals(writer.Project, project, StringComparison.OrdinalIgnoreCase));

        return [.. WrongBuildVerifiersOf(project), .. WrongWriters.Among(writers, of => _holdouts.LocationOfProject(of ?? string.Empty).Folder)];
    }

    private IReadOnlyList<WrongVerifier> WrongBuildVerifiersOf(string project)
    {
        var links = Chains?.AllLinks ?? [];
        var pending = ClearUp;
        var records = new List<VerifierRecord>();

        foreach (var card in Board.Cards.Where(card => card.Workstream.Role == WorkstreamRole.Verifier && pending?.Covers(card.Key) != true))
        {
            if (links.Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack
                                    && string.Equals(link.From, card.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(link => link.CreatedUtc).LastOrDefault() is not { } report
                || Board.Card(report.To) is not { } subject
                || !string.Equals(subject.Workstream.Project, project, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            records.Add(new VerifierRecord(card.Key, card.Title, subject.Key, subject.Workstream.Project, card.IsLive,
                card.Workstream.IsArchived, report.CreatedUtc, report.Output, report.SatisfiedBy == LinkBook.ConfirmedReport)
            {
                BriefNamesChecks = card.Workstream.Prompt.Contains(VerifierBrief.ChecksMarker, StringComparison.Ordinal),
                SubjectNamed = _holdouts.Names(subject.Workstream) != false,
                HasReport = report.LatestReport is not null,
            });
        }

        return WrongVerifiers.Among(records, HoldoutOwner);
    }

    /// <summary>
    /// The project a holdout folder is the own folder of: the one it was found beside the home repo of, by convention; else
    /// the one project that chose it in Options; null when none is, or when several chose it, as with the folder set for
    /// every project. Being found beside a repo comes first: a folder chosen for other projects too is still that repo's.
    /// </summary>
    private string? HoldoutOwner(string folder)
    {
        var projects = Board.Cards.Select(card => card.Workstream.Project).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // By name as well as by clone: a project's cards often start in a notes folder and list no clone to find (live read).
        if (projects.FirstOrDefault(project => HoldoutConventionOf(project) is var convention
                && (SameFolder(convention.BesideRepo, folder) || SameFolder(convention.RecordedFolder, folder))
                || (HomeRepo.NameIn(_projectReads.Current.FirstOrDefault(each => string.Equals(each.Name, project, StringComparison.OrdinalIgnoreCase))?.Text)
                    is { } repo && string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)), repo + HomeRepo.HoldoutSuffix,
                        StringComparison.OrdinalIgnoreCase))) is { } beside)
        {
            return beside;
        }

        var chosen = _settings.Read().ProjectHoldoutFolders;

        return projects.Where(project => SameFolder(chosen.GetValueOrDefault(project), folder)).ToList() is [var only] ? only : null;
    }

    private static bool SameFolder(string? left, string right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
        && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Now, for the clear-up's Undo; a test sets it.</summary>
    internal Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.Now;

    private static int RoundOf(ProjectFileReading? reading) => reading?.File?.Rounds.Max(round => round.Number) ?? 0;

    /// <summary>
    /// The notice a project's kept clear-up leaves while it can be undone, or null. Once it lapses (a day, or the next round)
    /// its record is dropped, so it cannot be undone any more; one still archiving is kept until it has finished.
    /// </summary>
    private string? ClearUpNoticeOf(ProjectFileReading reading)
    {
        if (_settings.Read().VerifierClearUp is not { } record || !string.Equals(record.Project, reading.Name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (record.Dated(Now(), RoundOf(reading)) is var dated && dated != record)
        {
            record = dated;
            _settings.Write(_settings.Read() with { VerifierClearUp = record });
        }

        var done = VerifierClearUp.From(record).IsDone;
        var notice = ClearUpNotice.For(record, Now(), RoundOf(reading));

        // Said only once every card is archived: until then "archived" is not yet true, and Dismiss would refuse (S3).
        if (notice is not null && !done)
        {
            return null;
        }

        if (notice is null && done)
        {
            _settings.Write(_settings.Read() with { VerifierClearUp = null });
            TheseusLog.Information("The clear-up of {Project} lapsed; it can no longer be undone", reading.Name);
        }

        return notice;
    }

    /// <summary>✕ Dismiss on the clear-up's notice: drops its record, so it can no longer be undone.</summary>
    private void DismissClearUp(string project)
    {
        if (_settings.Read().VerifierClearUp is not { } record || !string.Equals(record.Project, project, StringComparison.OrdinalIgnoreCase)
            || !VerifierClearUp.From(record).IsDone)
        {
            return;
        }

        _settings.Write(_settings.Read() with { VerifierClearUp = null });
        TheseusLog.Information("The clear-up of {Project} was dismissed", project);
        Refresh();
    }

    /// <summary>Shows the exact list, then on the owner's yes retires their links, stops the running ones, and archives each.</summary>
    private void ClearUpVerifiers(string project)
    {
        var listed = WrongVerifiersOf(project);

        if (listed.Count == 0)
        {
            Notice = "No verifier card needs clearing up.";

            return;
        }

        // A checklist, one row per card, each ticked to begin with (Nigel, 2026-11-23: "a very dense message box").
        var ticked = AskChecklist is { } ask
            ? ask(new ChecklistViewModel(Checklists.ClearUp(listed, key => Board.Card(key)?.Title ?? key)))
            : [.. listed.Select(verifier => verifier.Key)];

        if (ticked is not { Count: > 0 })
        {
            return;
        }

        // Worked out again after the question, so a card that started while it was open is stopped too (review of item 5, S8);
        // only the cards he ticked are taken.
        var seen = ticked.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wrong = WrongVerifiersOf(project).Where(verifier => seen.Contains(verifier.Key)).ToList();
        var earlier = ClearUp is { } kept && string.Equals(kept.Project, project, StringComparison.OrdinalIgnoreCase) ? kept.Record : null;
        var clearUp = VerifierClearUp.Plan(project, wrong, Chains?.AllLinks ?? [], Now().ToUniversalTime(), RoundOf(ProjectReadingOf(project)));
        var why = wrong.ToDictionary(verifier => verifier.Key, verifier => verifier.Why, StringComparer.OrdinalIgnoreCase);

        foreach (var link in clearUp.Before)
        {
            Chains?.Retire(link.Id, $"Cleared up: {why.GetValueOrDefault(link.From, "a verifier that should not be kept")}");
        }

        // A second press of the same project's line adds to the first, so the first's Undo is kept (review of item 5, M3).
        var record = earlier is null
            ? clearUp.Record
            : new VerifierClearUpRecord(project, [.. earlier.Cards, .. clearUp.Record.Cards], earlier.Archived, [.. earlier.Before, .. clearUp.Before])
            {
                AtUtc = clearUp.AtUtc,
                Round = clearUp.Round,
            };
        _settings.Write(_settings.Read() with { VerifierClearUp = record });

        var running = clearUp.ToStop.Select(Board.Card).OfType<WorkstreamCardViewModel>().ToList();

        if (running.Count > 0)
        {
            BeginStop(StepsFor(running), keepQueued: true);
        }

        TheseusLog.Information("Clearing up {Count} verifier card(s) of {Project}: {Keys}; stopping {Running}; retired {Links} link(s)",
            wrong.Count, project, string.Join(", ", wrong.Select(verifier => verifier.Key)), string.Join(", ", clearUp.ToStop), clearUp.Before.Count);

        ArchiveClearedUp();
        Refresh();
    }

    /// <summary>Archives each card of the kept clear-up whose session has gone; asked again on every refresh until all are.</summary>
    private void ArchiveClearedUp()
    {
        if (ClearUp is not { IsDone: false } clearUp)
        {
            return;
        }

        var due = clearUp.Due(key => Board.Card(key)?.IsLive ?? false);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var key in due)
        {
            if (Board.Card(key) is { IsArchived: false } card)
            {
                Board.SetArchived(card, true);
                TheseusLog.Information("Archived {Verifier}, cleared up", key);
            }
        }

        _settings.Write(_settings.Read() with { VerifierClearUp = clearUp.Record });

        if (clearUp.IsDone)
        {
            Notice = $"Cleared up {clearUp.Archived.Count} verifier {(clearUp.Archived.Count == 1 ? "card" : "cards")}.";
        }
    }

    /// <summary>Undo: restores the cards the clear-up archived and puts their report-back links back exactly as they were.</summary>
    private void UndoClearUp(string project)
    {
        if (ClearUp is not { } clearUp || !string.Equals(clearUp.Project, project, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var card in clearUp.Archived.Select(Board.Card).OfType<WorkstreamCardViewModel>().Where(card => card.IsArchived))
        {
            Board.SetArchived(card, false);
        }

        foreach (var link in clearUp.Before)
        {
            Chains?.Restore(link);
        }

        _settings.Write(_settings.Read() with { VerifierClearUp = null });
        TheseusLog.Information("Undid the clear-up of {Count} verifier card(s) of {Project}", clearUp.Record.Cards.Count, project);
        Notice = $"Restored {clearUp.Archived.Count} verifier {(clearUp.Archived.Count == 1 ? "card" : "cards")}; their links are as they were.";
        Refresh();
    }
}
