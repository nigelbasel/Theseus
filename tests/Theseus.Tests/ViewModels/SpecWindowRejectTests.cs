using System.Windows;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Reject… in the spec window, beside Accept spec… (board-small-changes item 4, Nigel, 2026-11-26): it asks for an
/// optional note, then rejects the version on screen, never one changed since it was drawn, and says what was done.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SpecWindowRejectTests
{
    private string _folder = string.Empty;
    private readonly List<(SpecAtGate Spec, string? Note)> _rejected = [];

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"theseus-spec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_folder);
        _rejected.Clear();
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void RejectDotDotDot_AsksForANote_AndRejectsNothingYet()
    {
        var model = Model(Write("v1"));

        model.AskRejectCommand.Execute(null);

        Assert.That((model.IsRejecting, model.AcceptCommand.CanExecute(null), _rejected.Count), Is.EqualTo((true, false, 0)));
    }

    [Test]
    public void RejectingIt_RejectsTheVersionOnScreen_WithTheNote_AndSaysWhatWasDone()
    {
        var spec = Write("v1");
        var model = Model(spec);
        model.AskRejectCommand.Execute(null);
        model.Note = "The second criterion cannot be tested.";

        model.RejectCommand.Execute(null);

        Assert.That(_rejected.Single(), Is.EqualTo((spec, (string?)"The second criterion cannot be tested.")));
        Assert.That((model.IsRejecting, model.Notice), Is.EqualTo((false, $"Rejected version {spec.ShortVersion}. pr5 is told.")));
    }

    [Test]
    public void AnEmptyNote_IsNoNote()
    {
        var model = Model(Write("v1"));
        model.AskRejectCommand.Execute(null);
        model.Note = "   ";

        model.RejectCommand.Execute(null);

        Assert.That(_rejected.Single().Note, Is.Null);
    }

    [Test]
    public void ASpecChangedSinceItWasDrawn_IsDrawnAgain_AndNotRejected()
    {
        var spec = Write("v1");
        var model = Model(spec);
        model.AskRejectCommand.Execute(null);
        File.WriteAllText(spec.FilePath!, "v2");

        model.RejectCommand.Execute(null);

        Assert.That((_rejected.Count, model.Text), Is.EqualTo((0, "v2")));
        Assert.That(model.Notice, Does.EndWith("Read it, then decide."));
    }

    [Test]
    public void ASpecAlreadyAcceptedAtThisVersion_CannotBeRejected()
    {
        var model = Model(Write("v1"), whyNot: _ => "Version 1 was accepted.");

        Assert.That(model.AskRejectCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void AVersionRejected_CannotBeRejectedAgain_UntilItChanges()
    {
        var spec = Write("v1");
        var model = Model(spec);
        model.AskRejectCommand.Execute(null);
        model.RejectCommand.Execute(null);

        Assert.That((model.CanReject, model.AskRejectCommand.CanExecute(null)), Is.EqualTo((false, false)));
        Assert.That(model.RejectToolTip, Is.EqualTo($"Version {spec.ShortVersion} was rejected; its session is told to write a new one"));

        File.WriteAllText(spec.FilePath!, "v2");
        model.Refresh();

        Assert.That(model.CanReject, Is.True, "a new version can be decided again");
    }

    [Test]
    public void AVersionAcceptedWhileTheNoteWasOpen_CannotThenBeRejected()
    {
        string? accepted = null;
        var model = Model(Write("v1"), whyNot: _ => accepted);
        model.AskRejectCommand.Execute(null);

        accepted = "Version 1 was accepted.";
        model.Refresh();

        Assert.That(model.RejectCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void Cancel_PutsTheNoteAway_RejectsNothing_AndAcceptCanRunAgain()
    {
        var model = Model(Write("v1"));
        model.AskRejectCommand.Execute(null);
        model.Note = "half a thought";

        model.CancelRejectCommand.Execute(null);

        Assert.That((model.IsRejecting, model.Note, _rejected.Count, model.AcceptCommand.CanExecute(null)),
            Is.EqualTo((false, string.Empty, 0, true)));
    }

    [Test]
    public void ARulesWindow_SaysRule()
    {
        File.WriteAllText(Path.Combine(_folder, "fix.md"), "## Rule\n\nIt holds.");
        var rule = new SpecAtGate("theseus", "bug1", SpecIdentity.Hash("It holds."), "It holds.", new WorkstreamSource(_folder)) { IsRule = true };
        var model = Model(rule);

        Assert.That(model.RejectToolTip, Is.EqualTo("Send the rule back to its session to be written again, with a note; asks for the note first. Nothing is started"));
    }

    [Test]
    public void TheWindow_HasRejectBesideAccept_AndItsNotePanelOnlyWhileRejecting()
    {
        var window = (SpecWindow)Theseus.Tests.Windows.DialogHost.Open(new SpecWindow(Model(Write("v1"))));

        try
        {
            Assert.That((window.RejectButton.Content, window.RejectButton.Visibility, window.RejectPanel.Visibility),
                Is.EqualTo(("_Reject…", Visibility.Visible, Visibility.Collapsed)));

            window.Model.AskRejectCommand.Execute(null);

            Assert.That(window.RejectPanel.Visibility, Is.EqualTo(Visibility.Visible));
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void AWindowWithNoReject_HasNoRejectButton()
    {
        var spec = Write("v1");
        var window = (SpecWindow)Theseus.Tests.Windows.DialogHost.Open(
            new SpecWindow(new SpecWindowViewModel(spec, shown => shown.ReadAgain(), _ => null, _ => true, _ => { })));

        try
        {
            Assert.That(window.RejectButton.Visibility, Is.EqualTo(Visibility.Collapsed));
        }
        finally
        {
            window.Close();
        }
    }

    private SpecAtGate Write(string text)
    {
        File.WriteAllText(Path.Combine(_folder, "spec.md"), text);

        return new SpecAtGate("theseus", "pr5", SpecIdentity.Hash(text), text, new WorkstreamSource(_folder));
    }

    private SpecWindowViewModel Model(SpecAtGate spec, Func<SpecAtGate, string?>? whyNot = null) =>
        new(spec, shown => shown.ReadAgain(), whyNot ?? (_ => null), _ => true, _ => { }, (shown, note) =>
        {
            _rejected.Add((shown, note));

            return $"{shown.Slug} is told.";
        });
}
