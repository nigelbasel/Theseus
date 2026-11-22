using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The spec window (1.1e AC 24a, 25): it draws the spec, Accept accepts the version shown, a file changed since it was
/// drawn is drawn again before it can be accepted, and Accept is disabled with its reason once accepted at that version.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SpecWindowTests
{
    private string _folder = string.Empty;

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"theseus-spec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void ItIsTitledWithThePieceOfWorkAndItsVersion_AndDrawsTheSpec()
    {
        var spec = Write("# Spec\n\nIt reads.");
        var window = new SpecWindow(Model(spec, out _));

        Assert.That(window.Title, Is.EqualTo($"Spec of pr5 in theseus, version {spec.ShortVersion}"));
        Assert.That(new TextRange(window.PreviewView.Document.ContentStart, window.PreviewView.Document.ContentEnd).Text, Does.Contain("It reads."));
        Assert.That(window.SourceView.Visibility, Is.EqualTo(Visibility.Collapsed));

        window.Model.SourceCommand.Execute(null);

        Assert.That((window.PreviewView.Visibility, window.SourceView.Visibility, window.SourceView.Text),
            Is.EqualTo((Visibility.Collapsed, Visibility.Visible, "# Spec\n\nIt reads.")));
    }

    [Test]
    public void Accept_AcceptsTheVersionOnScreen()
    {
        var spec = Write("v1");
        var model = Model(spec, out var accepted);

        model.AcceptCommand.Execute(null);

        Assert.That(accepted.Single().ContentHash, Is.EqualTo(spec.ContentHash));
        Assert.That(model.Notice, Is.EqualTo($"Accepted version {spec.ShortVersion}."));
    }

    [Test]
    public void ASpecChangedSinceItWasDrawn_SaysSo_IsDrawnAgain_AndIsNotAccepted()
    {
        var spec = Write("v1");
        var model = Model(spec, out var accepted);
        var redrawn = 0;
        model.Redrawn += (_, _) => redrawn++;
        File.WriteAllText(spec.FilePath!, "v2");

        model.AcceptCommand.Execute(null);

        Assert.That(accepted, Is.Empty, "never the version not on screen");
        Assert.That((model.Text, redrawn), Is.EqualTo(("v2", 1)));
        Assert.That(model.Notice, Does.StartWith($"The spec changed since it was drawn (version {spec.ShortVersion})").And.EndWith("Read it, then accept."));

        model.AcceptCommand.Execute(null);

        Assert.That(accepted.Single().ContentHash, Is.EqualTo(model.Shown.ContentHash), "the redrawn version, once read");
    }

    [Test]
    public void TheFileChanging_RedrawsIt_AndSaysSo()
    {
        var spec = Write("v1");
        var model = Model(spec, out _);
        File.WriteAllText(spec.FilePath!, "v2");

        model.Refresh();

        Assert.That((model.Text, model.Title.EndsWith(model.Shown.ShortVersion, StringComparison.Ordinal)), Is.EqualTo(("v2", true)));
        Assert.That(model.Notice, Does.StartWith("The spec changed on disk"));
    }

    [Test]
    public void AlreadyAcceptedAtThisVersion_AcceptIsDisabled_WithTheReason()
    {
        var spec = Write("v1");
        var model = Model(spec, out _, whyNot: shown => shown.ContentHash == spec.ContentHash ? "Version accepted already" : null);

        Assert.That((model.CanAccept, model.AcceptCommand.CanExecute(null), model.AcceptToolTip), Is.EqualTo((false, false, "Version accepted already")));

        File.WriteAllText(spec.FilePath!, "v2");
        model.Refresh();

        Assert.That(model.CanAccept, Is.True, "a changed spec can be accepted again");
    }

    [Test]
    public void AlreadyAccepted_ByTheRealStore_SaysWhenAndIsDisabled()
    {
        var spec = Write("v1");
        var store = new SpecAcceptanceStore(Path.Combine(_folder, "acceptances.json"));
        var model = new SpecWindowViewModel(spec, shown => shown.ReadAgain(), store.WhyNotAccept,
            shown => store.AcceptIfConfirmed(shown, _ => true, DateTimeOffset.UtcNow), _ => { });

        model.AcceptCommand.Execute(null);

        Assert.That(model.CanAccept, Is.False);
        Assert.That(model.AcceptToolTip, Does.StartWith($"Version {spec.ShortVersion} was accepted on ").And.EndWith("until the spec changes"));
    }

    [Test]
    public void ASpecGoneFromDisk_CannotBeAccepted()
    {
        var spec = Write("v1");
        var model = Model(spec, out var accepted);
        File.Delete(spec.FilePath!);

        model.Refresh();
        model.AcceptCommand.Execute(null);

        Assert.That((model.CanAccept, accepted.Count), Is.EqualTo((false, 0)));
        Assert.That(model.AcceptToolTip, Is.EqualTo("The spec is no longer on disk, so there is nothing to accept"));
    }

    [Test]
    public void EnterAndF3_FindTheNextMatch_AndCtrlFComesBackToTheSearch_FromTheKeys()
    {
        var window = new SpecWindow(Model(Write("alpha **beta** alpha"), out _)) { Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false };
        window.Show();

        try
        {
            window.Model.SourceCommand.Execute(null);
            window.Model.Search = "ALPHA";

            Press(window.SearchBox, Key.Enter);
            var first = window.SourceView.SelectionStart;
            Press(window.SearchBox, Key.Enter);
            var second = window.SourceView.SelectionStart;
            Press(window.SearchBox, Key.F3);

            Assert.That((first, second, window.SourceView.SelectionStart, window.FindResult.Text), Is.EqualTo((0, 15, 0, "2 matches")));

            window.Model.PreviewCommand.Execute(null);
            window.Model.Search = "beta";
            Press(window.SearchBox, Key.Enter);

            Assert.That((window.FindResult.Text, window.PreviewView.Selection?.Text), Is.EqualTo(("1 match", "beta")), "counted in what is drawn");

            window.PreviewView.Focus();
            Press(window.PreviewView, Key.F, ModifierKeys.Control);

            Assert.That(window.SearchBox.IsKeyboardFocusWithin, Is.True, "Ctrl+F from the preview");
        }
        finally
        {
            window.Close();
        }
    }

    // A key pressed on an element of a shown window, through the window's preview and the element's own handlers.
    private static void Press(UIElement target, Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        var source = PresentationSource.FromVisual(target)!;
        var preview = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = UIElement.PreviewKeyDownEvent,
        };

        if (modifiers == ModifierKeys.None)
        {
            target.RaiseEvent(preview);

            if (!preview.Handled)
            {
                target.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, key)
                {
                    RoutedEvent = UIElement.KeyDownEvent,
                });
            }

            return;
        }

        // Keyboard.Modifiers reads the real keyboard, which a test cannot hold down: the window's handler is reached directly.
        ((SpecWindow)Window.GetWindow(target)!).HandleKey(key, modifiers);
    }

    private SpecAtGate Write(string text)
    {
        File.WriteAllText(Path.Combine(_folder, "spec.md"), text);

        return new SpecAtGate("theseus", "pr5", SpecIdentity.Hash(text), text, new WorkstreamSource(_folder));
    }

    private static SpecWindowViewModel Model(SpecAtGate spec, out List<SpecAtGate> accepted, Func<SpecAtGate, string?>? whyNot = null)
    {
        var taken = new List<SpecAtGate>();
        accepted = taken;

        return new SpecWindowViewModel(spec, shown => shown.ReadAgain(), whyNot ?? (_ => null), shown =>
        {
            taken.Add(shown);

            return true;
        }, _ => { });
    }
}
