using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.Tests.Projects;

/// <summary>
/// A project file that needs updating (1.1e AC 18 to 21): the face's words for each state, the updater's opening prompt,
/// Update refused while one of the project's cards runs, and a folder hidden as not a project and shown again.
/// </summary>
[TestFixture]
public class ProjectUpdateTests
{
    private const string Problem = "Does not read: line 3: the Kit line names method-v0.1";

    [TestCase(false, false, "Needs updating")]
    [TestCase(true, false, "Updating…")]
    [TestCase(true, true, "Updating…")]
    [TestCase(false, true, "Still needs updating")]
    public void TheFace_SaysOnlyWhereTheUpdateIs(bool running, bool ranSinceItBroke, string face)
    {
        Assert.That(ProjectUpdaterCard.Face(running, ranSinceItBroke), Is.EqualTo(face));
    }

    [Test]
    public void TheProblem_IsTheReadingsReasonWithItsLine_OrThePinnedKit()
    {
        Assert.That(ProjectUpdate.ProblemOf(ProjectFileReading.Unreadable("bym", "line 3: the Kit line names method-v0.1")), Is.EqualTo(Problem));
        Assert.That(ProjectUpdate.ProblemOf(ProjectFileReading.Unreadable("bym", "x") with { Status = ProjectReadingStatus.Pinned }),
            Is.EqualTo($"Pinned to {MethodKits.Pinned}: its project.md is in the older shape"));
    }

    [Test]
    public void TheUpdatersPrompt_NamesTheFileItsProblemAndTheShape_AndKeepsItToTheShape()
    {
        var updater = ProjectUpdaterCard.For("bym", @"<projects>ym", Problem + ".", ProjectStarter.ShapeText());

        Assert.That(updater.Title, Is.EqualTo("Update the project file of bym"));
        Assert.That(updater.WorkingDirectory, Is.EqualTo(@"<projects>ym"));
        Assert.That(updater.Prompt, Does.StartWith($@"Theseus cannot read <projects>ym\project.md: {Problem}.{Environment.NewLine}"), "one full stop");
        Assert.That(updater.Prompt, Does.Contain("Change its shape only").And.Contain("Decisions table").And.Contain("by \"Update the project file of bym\"")
            .And.Contain("exit once the file has changed"));
        Assert.That(RoleEnvironment.For(ProjectUpdaterCard.Role), Is.Empty, "it is no holdout author");
    }

    [Test]
    public void TheShape_IsTheMethodsDescription_WithWhatIsReadAndTheRules_NotTheGates()
    {
        var shape = ProjectStarter.ShapeText();

        Assert.That(shape, Does.StartWith("## Shape").And.Contain("## What is read").And.Contain("## Rules").And.Contain("**Pipeline:**"));
        Assert.That(shape, Does.Not.Contain("## The owner's gates"));
    }

    [Test]
    public void ACardStaysTheProjects_AfterItsFileStopsReading()
    {
        var history = new ProjectMemberHistory();
        history.Remember(ProjectMembers.Remembered(new Dictionary<string, ProjectMember> { ["w7"] = new("bym", "w7") }));
        history.Remember(ProjectMembers.None);

        Assert.That(ProjectUpdate.IsMember("w7", @"E:\work\w7
epo", history.CardsOf("bym"), @"<projects>ym"), Is.True, "named before");
        Assert.That(ProjectUpdate.IsMember("other", @"<projects>ym\", history.CardsOf("bym"), @"<projects>ym"), Is.True, "works in its folder");
        Assert.That(ProjectUpdate.IsMember("other", @"E:\work\other", history.CardsOf("bym"), @"<projects>ym"), Is.False);
    }

    [Test]
    public void AnUpdaterIsAskedToStop_OnlyOnceTheFileHasChanged_AndItsTurnHasEnded()
    {
        var started = ProjectUpdate.Fingerprint("# bym");
        var changed = ProjectFileReading.Unreadable("bym", "x") with { Text = "# bym, half converted" };
        var same = ProjectFileReading.Unreadable("bym", "x") with { Text = "# bym" };

        Assert.That(ProjectUpdate.IsDone(started, changed, turnEnded: false), Is.False, "still writing");
        Assert.That(ProjectUpdate.IsDone(started, same, turnEnded: true), Is.False, "nothing changed");
        Assert.That(ProjectUpdate.IsDone(started, changed, turnEnded: true), Is.True);
        Assert.That(ProjectUpdate.IsDone(string.Empty, changed, turnEnded: true), Is.False, "asked already");
    }

    [Test]
    public void ABrokenProjectsMembers_AreRememberedForTheNextRun_AndAGoneProjectsAreNot()
    {
        var before = ProjectMembers.Remembered(new Dictionary<string, ProjectMember>
        {
            ["w7"] = new("bym", "w7"),
            ["old"] = new("gone", "old"),
        });
        IReadOnlyList<ProjectFileReading> readings = [ProjectFileReading.Unreadable("bym", "line 3")];

        var kept = ProjectUpdate.KeepBroken(ProjectMembers.None, before, readings);

        Assert.That(kept.ByCard.Keys, Is.EquivalentTo(new[] { "w7" }));
    }

    [Test]
    public void AHiddenFolderThatReads_LeavesTheList()
    {
        Assert.That(ProjectUpdate.StillHidden(["notes", "bym"], ["BYM", "theseus"]), Is.EqualTo(new[] { "notes" }));
    }

    [Test]
    public void Update_WaitsWhileOneOfTheProjectsCardsRuns_OrTheUpdaterDoes()
    {
        IReadOnlyList<(string, bool, string?)> cards = [("W7", true, "bym"), ("W8", false, "bym"), ("Other", true, "theseus")];

        Assert.That(ProjectUpdate.Refusal("bym", cards, updaterRunning: false), Does.StartWith("W7 of bym is running; Update waits"));
        Assert.That(ProjectUpdate.Refusal("bym", [("W8", false, "bym"), ("Other", true, "theseus")], updaterRunning: false), Is.Null);
        Assert.That(ProjectUpdate.Refusal("bym", [], updaterRunning: true), Is.EqualTo("bym's project.md is being updated already."));
    }

    [Test]
    public void AFolderSaidNotToBeAProject_IsListedInOptions_AndShownAgainOnceSaved()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"theseus-hidden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            var store = new SettingsStore(Path.Combine(folder, "settings.json"));
            store.Write(store.Read() with { HiddenProjectFolders = ["leadpro-mcp", "notes"] });
            var options = new SettingsViewModel(store, () => { });

            options.OpenCommand.Execute(null);

            Assert.That(options.HiddenProjectFolders, Is.EqualTo(new[] { "leadpro-mcp", "notes" }));

            options.ShowProjectFolderCommand.Execute("notes");
            options.SaveCommand.Execute(null);

            Assert.That(store.Read().HiddenProjectFolders, Is.EqualTo(new[] { "leadpro-mcp" }));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void ProjectProperties_ShowsWhyTheFileDoesNotRead()
    {
        var properties = new ProjectPropertiesViewModel("bym", "bym", "#2F7D32", @"<projects>\bym\project.md")
        {
            Problem = "Pinned to method-v0.1: its project.md is in the older shape",
        };

        Assert.That((properties.HasProblem, properties.Problem), Is.EqualTo((true, (string?)"Pinned to method-v0.1: its project.md is in the older shape")));
        Assert.That(new ProjectPropertiesViewModel("t", "T", "#2F7D32", "f").HasProblem, Is.False);
    }
}
