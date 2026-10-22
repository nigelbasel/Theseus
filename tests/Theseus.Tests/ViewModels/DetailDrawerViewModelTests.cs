using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The details are in tabs (Nigel, 2026-10-22). What is going on with a card applies the moment it is
/// changed; how its sessions start is saved with Save, and only from the Setup tab. One Save for
/// everything lost a half-made link without a word, because it also closed the drawer.
/// </summary>
[TestFixture]
public class DetailDrawerViewModelTests
{
    private BoardFixture _fixture = null!;
    private DetailDrawerViewModel _drawer = null!;
    private int _saves;

    [SetUp]
    public void SetUp()
    {
        _fixture = new BoardFixture().With("one").With("two");
        _fixture.Board.Refresh();
        _saves = 0;
        _drawer = new DetailDrawerViewModel(_fixture.Plans, new NoRuns(), new NoWorkflows(), card =>
        {
            _fixture.Board.Persist(card);
            _saves++;
        });
    }

    [Test]
    public void Show_OpensOnOverview()
    {
        _drawer.Show(Card("one"));

        Assert.That(_drawer.SelectedTab, Is.EqualTo(DrawerTab.Overview));
    }

    [Test]
    public void Show_CanOpenOnAnotherTab()
    {
        _drawer.Show(Card("one"), DrawerTab.Links);

        Assert.That(_drawer.SelectedTab, Is.EqualTo(DrawerTab.Links));
    }

    [Test]
    public void Blocked_IsSavedTheMomentItIsTicked()
    {
        _drawer.Show(Card("one"));

        _drawer.IsBlocked = true;

        Assert.That(Card("one").Workstream.IsBlocked, Is.True);
        Assert.That(_saves, Is.EqualTo(1));
        Assert.That(_drawer.IsOpen, Is.True, "nothing on Overview closes the drawer");
    }

    [Test]
    public void ReopenTomorrow_IsSavedTheMomentItIsTicked()
    {
        _drawer.Show(Card("one"));

        _drawer.ReopenTomorrow = false;

        Assert.That(Card("one").Workstream.ReopenTomorrow, Is.False);
    }

    [Test]
    public void Title_IsSavedWhenChanged()
    {
        _drawer.Show(Card("one"));

        _drawer.Title = "Renamed";

        Assert.That(_fixture.Plans.TryRead("one")!.Title, Is.EqualTo("Renamed"));
    }

    [Test]
    public void AJiraKey_IsSavedWhenAdded()
    {
        _drawer.Show(Card("one"));
        _drawer.NewJiraKey = "NDC-1";

        _drawer.AddJiraKeyCommand.Execute(null);

        Assert.That(Card("one").Workstream.JiraKeys, Is.EquivalentTo(new[] { "NDC-1" }));
    }

    [Test]
    public void Showing_SavesNothing()
    {
        _drawer.Show(Card("one"));

        Assert.That(_saves, Is.Zero);
    }

    [Test]
    public void AnOverviewChange_LeavesUnsavedSetupAlone()
    {
        _drawer.Show(Card("one"));
        _drawer.WorkingDirectory = @"C:\elsewhere";

        _drawer.IsBlocked = true;

        Assert.That(_fixture.Plans.TryRead("one")!.Cwd, Is.EqualTo(@"C:\work"));
        Assert.That(_drawer.IsSetupDirty, Is.True);
    }

    [Test]
    public void EditingSetup_MarksItUnsaved_AndPuttingItBackDoesNot()
    {
        _drawer.Show(Card("one"));

        _drawer.EveryStartPrompt = "Run the tests.";
        Assert.That(_drawer.IsSetupDirty, Is.True);

        _drawer.EveryStartPrompt = string.Empty;
        Assert.That(_drawer.IsSetupDirty, Is.False);
    }

    [Test]
    public void Save_WritesTheSetupAndKeepsTheDrawerOpen()
    {
        _drawer.Show(Card("one"));
        _drawer.WorkingDirectory = @"C:\elsewhere";
        _drawer.EveryStartPrompt = "Run the tests.";

        _drawer.SaveCommand.Execute(null);

        Assert.That(_fixture.Plans.TryRead("one")!.Cwd, Is.EqualTo(@"C:\elsewhere"));
        Assert.That(Card("one").Workstream.EveryStartPrompt, Is.EqualTo("Run the tests."));
        Assert.That(_drawer.IsSetupDirty, Is.False);
        Assert.That(_drawer.IsOpen, Is.True);
    }

    [Test]
    public void ShowingAnotherCard_WithUnsavedSetup_AsksFirst()
    {
        _drawer.Show(Card("one"));
        _drawer.WorkingDirectory = @"C:\elsewhere";

        _drawer.Show(Card("two"));

        Assert.That(_drawer.IsConfirmingDiscard, Is.True);
        Assert.That(_drawer.Key, Is.EqualTo("one"));
    }

    [Test]
    public void Discard_ThrowsTheEditsAwayAndCarriesOn()
    {
        _drawer.Show(Card("one"));
        _drawer.WorkingDirectory = @"C:\elsewhere";
        _drawer.Show(Card("two"));

        _drawer.DiscardCommand.Execute(null);

        Assert.That(_drawer.Key, Is.EqualTo("two"));
        Assert.That(_fixture.Plans.TryRead("one")!.Cwd, Is.EqualTo(@"C:\work"));
        Assert.That(_drawer.IsConfirmingDiscard, Is.False);
    }

    [Test]
    public void SaveAndContinue_SavesTheEditsAndCarriesOn()
    {
        _drawer.Show(Card("one"));
        _drawer.WorkingDirectory = @"C:\elsewhere";
        _drawer.Show(Card("two"));

        _drawer.SaveAndContinueCommand.Execute(null);

        Assert.That(_drawer.Key, Is.EqualTo("two"));
        Assert.That(_fixture.Plans.TryRead("one")!.Cwd, Is.EqualTo(@"C:\elsewhere"));
    }

    [Test]
    public void KeepEditing_StaysWhereItWas()
    {
        _drawer.Show(Card("one"));
        _drawer.WorkingDirectory = @"C:\elsewhere";
        _drawer.Show(Card("two"));

        _drawer.KeepEditingCommand.Execute(null);

        Assert.That(_drawer.Key, Is.EqualTo("one"));
        Assert.That(_drawer.WorkingDirectory, Is.EqualTo(@"C:\elsewhere"));
        Assert.That(_drawer.IsConfirmingDiscard, Is.False);
    }

    [Test]
    public void Close_WithUnsavedSetup_AsksFirst()
    {
        _drawer.Show(Card("one"));
        _drawer.WorkingDirectory = @"C:\elsewhere";

        _drawer.CloseCommand.Execute(null);

        Assert.That(_drawer.IsOpen, Is.True);
        Assert.That(_drawer.IsConfirmingDiscard, Is.True);

        _drawer.DiscardCommand.Execute(null);

        Assert.That(_drawer.IsOpen, Is.False);
    }

    [Test]
    public void Close_WithNothingUnsaved_Closes()
    {
        _drawer.Show(Card("one"));

        _drawer.CloseCommand.Execute(null);

        Assert.That(_drawer.IsOpen, Is.False);
    }

    private WorkstreamCardViewModel Card(string key) => _fixture.Card(key)!;

    private sealed class NoRuns : IRunLog
    {
        public void Append(SessionRun run)
        {
        }

        public void AppendEnd(RunEnd end)
        {
        }

        public IReadOnlyList<SessionRun> Read() => [];
    }

    private sealed class NoWorkflows : IWorkflowRunReader
    {
        public IReadOnlyList<WorkflowRun> Read(string? sessionId) => [];
    }
}
