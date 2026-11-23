using System.IO;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Clear up verifiers… (git-22, 2026-11-23): one press stops, rejects and archives the verifier cards that should not be
/// kept, which by hand was open, Stop, wait, ⋯ › Archive for every card; and Undo, while nothing else has been cleared up.
/// </summary>
public sealed partial class ShellViewModel
{
    private VerifierClearUp? _clearUp;
    private string? _clearUpProject;
    private IReadOnlyList<string> _clearUpRejected = [];

    /// <summary>The verifier cards of a project's builds that should not be kept.</summary>
    private IReadOnlyList<WrongVerifier> WrongVerifiersOf(string project)
    {
        var links = Chains?.AllLinks ?? [];
        var records = new List<VerifierRecord>();

        foreach (var card in Board.Cards.Where(card => card.Workstream.Role == WorkstreamRole.Verifier))
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
                card.Workstream.IsArchived, report.CreatedUtc, report.Output, report.SatisfiedBy == LinkBook.ConfirmedReport));
        }

        return WrongVerifiers.Among(records, key => Board.Card(key) is { } subject ? HoldoutOf(subject).Folder : null, HoldoutOwner);
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
        if (projects.FirstOrDefault(project => SameFolder(HoldoutBesideRepo(project), folder)
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
        !string.IsNullOrWhiteSpace(left)
        && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>How many cards the last clear-up of a project took away, while it can be undone.</summary>
    private int ClearedVerifiersOf(string project) =>
        _clearUp is not null && string.Equals(_clearUpProject, project, StringComparison.OrdinalIgnoreCase) ? _clearUp.Archived.Count : 0;

    /// <summary>Shows the exact list, then on the owner's yes rejects their reports, stops the running ones, and archives each.</summary>
    private void ClearUpVerifiers(string project)
    {
        var wrong = WrongVerifiersOf(project);

        if (wrong.Count == 0)
        {
            Notice = "No verifier card needs clearing up.";

            return;
        }

        var question = $"Clear up {wrong.Count} verifier {(wrong.Count == 1 ? "card" : "cards")}? Each running one is stopped first; "
                       + "then each is archived and its report rejected, so it never counts as met.\n"
                       + string.Join("\n", wrong.Select(verifier =>
                           $"  • {verifier.Title} ({(verifier.IsRunning ? "running" : "not running")}): {verifier.Why}"))
                       + "\nThe newest correct verifier of each build is kept. You can undo this.";

        if (ConfirmAcceptSpec?.Invoke(question) == false)
        {
            return;
        }

        var clearUp = VerifierClearUp.Plan(wrong, Chains?.AllLinks ?? []);
        var why = wrong.ToDictionary(verifier => verifier.Key, verifier => verifier.Why, StringComparer.OrdinalIgnoreCase);
        var rejected = new List<string>();

        foreach (var link in (Chains?.AllLinks ?? []).Where(link => clearUp.ReportsToReject.Contains(link.Id)))
        {
            if (Chains?.Reject(link.Id, $"Cleared up: {why.GetValueOrDefault(link.From, "a verifier that should not be kept")}") is null)
            {
                rejected.Add(link.Id);
            }
        }

        _clearUp = clearUp;
        _clearUpProject = project;
        _clearUpRejected = rejected;

        var running = clearUp.ToStop.Select(Board.Card).OfType<WorkstreamCardViewModel>().ToList();

        if (running.Count > 0)
        {
            BeginStop(StepsFor(running));
        }

        TheseusLog.Information("Clearing up {Count} verifier card(s) of {Project}: {Keys}; stopping {Running}; rejected {Rejected}",
            wrong.Count, project, string.Join(", ", wrong.Select(verifier => verifier.Key)), string.Join(", ", clearUp.ToStop), rejected.Count);

        ArchiveClearedUp();
        Refresh();
    }

    /// <summary>Archives each card of the clear-up whose session has gone; asked again on every refresh until all are.</summary>
    private void ArchiveClearedUp()
    {
        if (_clearUp is not { IsDone: false } clearUp)
        {
            return;
        }

        foreach (var key in clearUp.Due(key => Board.Card(key)?.IsLive ?? false))
        {
            if (Board.Card(key) is { IsArchived: false } card)
            {
                Board.SetArchived(card, true);
                TheseusLog.Information("Archived {Verifier}, cleared up", key);
            }
        }

        if (clearUp.IsDone)
        {
            Notice = $"Cleared up {clearUp.Archived.Count} verifier {(clearUp.Archived.Count == 1 ? "card" : "cards")}.";
        }
    }

    /// <summary>Undo: restores the cards the last clear-up archived and takes back the rejections it made.</summary>
    private void UndoClearUp(string project)
    {
        if (_clearUp is not { } clearUp || !string.Equals(_clearUpProject, project, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var card in clearUp.Archived.Select(Board.Card).OfType<WorkstreamCardViewModel>().Where(card => card.IsArchived))
        {
            Board.SetArchived(card, false);
        }

        foreach (var id in _clearUpRejected)
        {
            Chains?.Unreject(id);
        }

        TheseusLog.Information("Undid the clear-up of {Count} verifier card(s) of {Project}", clearUp.Archived.Count, project);
        Notice = $"Restored {clearUp.Archived.Count} verifier {(clearUp.Archived.Count == 1 ? "card" : "cards")}; their reports wait for you again.";
        _clearUp = null;
        _clearUpProject = null;
        _clearUpRejected = [];
        Refresh();
    }
}
