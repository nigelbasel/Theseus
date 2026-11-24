using System.IO;
using System.Windows;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Core.Proposals;

namespace Theseus.Tests.Windows;

/// <summary>
/// The secondary windows of Windows conventions batch WB5, each built over a small model of its own, so a test (or an
/// off-screen capture) can open every one of them without the board behind it. Nothing here reads the real data folder.
/// </summary>
public static class SecondaryWindows
{
    /// <summary>The name each window is known by in a test's message, with the function that builds it.</summary>
    public static IReadOnlyList<(string Name, Func<Window> Build)> All(string scratchFolder) =>
    [
        ("About", () => new AboutWindow()),
        ("AddObjective", () => new AddObjectiveWindow()),
        ("AddToProject", () => new AddToProjectWindow([new ProjectOption("theseus", "Theseus", [("1.9", "Windows conventions")])], "wg-secondary-windows")),
        ("Arrange", () => new ArrangeWindow { DataContext = new ArrangeViewModel() }),
        ("Checklist", () => new ChecklistWindow(Checklist())),
        ("DayReport", () => new DayReportWindow()),
        ("Instructions", () => new InstructionsWindow()),
        ("NewProject", () => new NewProjectWindow("my-project", "My project")),
        ("NewWorkstream", () => new NewWorkstreamWindow()),
        ("PromptEdit", () => new PromptEditWindow("Identity prompt", "What a session is told about itself when it starts.", "You are {title}.")),
        ("Proposal", () => new ProposalWindow(Proposal())),
        ("Records", () => new RecordsWindow(Records())),
        ("Spec", () => new SpecWindow(Spec(scratchFolder))),
    ];

    /// <summary>A two-row clear-up list, both rows ticked.</summary>
    public static ChecklistViewModel Checklist() =>
        new(new Checklist("Clear up", "These cards are done. Untick any you want to keep on the board.", "Stop and archive",
            [new ChecklistRow("a", "Auth research", "Done 2 days ago.", "Its session is still open."), new ChecklistRow("b", "Cache spike", "Done today.")],
            ticked => $"{ticked.Count} cards will be stopped and archived."));

    /// <summary>A pending proposal with a short brief.</summary>
    public static ProposalWindowViewModel Proposal() =>
        new(new WorkstreamProposal("pr-1", ProposalStatus.Pending, "auth-research", "s",
                new ProposalRequest("Cache spike", "brief.md", true, null, "A verdict.", [], null), [], DateTimeOffset.UnixEpoch),
            "Auth research", "# Brief\n\nFind out whether the cache helps.\n", _ => null, _ => null, (_, _) => null);

    /// <summary>One written record.</summary>
    public static RecordsViewModel Records() =>
        new("Cache spike: records", new RecordEntry("Plan", "How the work is split", StageNodeState.Done, "plan.md") { StateText = "Written" }, _ => { });

    /// <summary>A spec in <paramref name="folder"/>, which must exist: the window watches it for changes.</summary>
    public static SpecWindowViewModel Spec(string folder)
    {
        const string Text = "# Spec\n\nThe cache is read before the store.\n";
        File.WriteAllText(Path.Combine(folder, "spec.md"), Text);
        SpecAtGate spec = new("theseus", "cache-spike", SpecIdentity.Hash(Text), Text, new WorkstreamSource(folder));

        return new SpecWindowViewModel(spec, shown => shown.ReadAgain(), _ => null, _ => true, _ => { });
    }
}
