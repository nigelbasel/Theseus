using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Repository;

/// <summary>
/// The confirmations and message boxes (baseline WB3; W10, W12, W17, W43; checklist G7 to G16, G66, G83): a question names
/// its actions in its buttons, Cancel is the default and Esc, an information box has one Close, and a prompt keeps a
/// refused answer open with the reason.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ConfirmationDialogsTests
{
    [Test]
    public void AQuestionWithTwoActions_HasThemAndCancel_AndCancelIsBothTheDefaultAndEsc()
    {
        QuestionWindow window = new("Start Auth, which you paused?", "Build wants to start Auth.", "_Allow and start", "_Refuse", "Decide later");
        List<Button> buttons = Buttons(window);

        Assert.That(buttons.Select(button => button.Content), Is.EqualTo(new object[] { "_Allow and start", "_Refuse", "Decide later" }),
            "the actions on the left, Cancel on the right (W9)");
        Button cancel = buttons[^1];
        Assert.That((cancel.IsCancel, cancel.IsDefault), Is.EqualTo((true, true)), "Enter and Esc both choose Decide later (A3)");
        Assert.That(buttons.Take(2).Any(button => button.IsCancel || button.IsDefault), Is.False);
        Assert.That(window.Answer, Is.Null, "nothing chosen yet");
    }

    [Test]
    public void EachActionOfAQuestion_GivesItsOwnAnswer()
    {
        QuestionWindow first = new("Start paused or blocked work?", "Auth is marked paused.", "_Clear marks and start", "_Start only the others");
        QuestionWindow second = new("Start paused or blocked work?", "Auth is marked paused.", "_Clear marks and start", "_Start only the others");

        Click(Buttons(first)[0]);
        Click(Buttons(second)[1]);

        Assert.That((first.Answer, second.Answer), Is.EqualTo(((bool?)true, (bool?)false)));
    }

    [Test]
    public void AQuestion_UsesNoAccentButton_SinceItsDefaultIsCancel()
    {
        // The source, not the built window: no Application runs in tests, so every DynamicResource style there is null.
        System.Xml.Linq.XDocument markup = System.Xml.Linq.XDocument.Load(Theseus.Tests.Conventions.LooseXaml.SourceFile("QuestionWindow.xaml"));
        List<string?> styles = [.. markup.Descendants().Where(element => element.Name.LocalName == "Button")
            .Select(button => (string?)button.Attribute("Style"))];
        QuestionWindow window = new("Accept spec?", "Accept the spec of auth?", "_Accept spec");

        Assert.That(styles, Has.Count.EqualTo(3));
        Assert.That(styles, Is.All.EqualTo("{DynamicResource DialogButton}"), "W12: no accent where Cancel is the default on purpose");
        Assert.That(Buttons(window).Select(button => button.Content), Is.EqualTo(new object[] { "_Accept spec", "Cancel" }));
    }

    [Test]
    public void ProjectProperties_DropsAStagedRemote_WhenTheHoldoutFolderIsChangedOrReset()
    {
        List<string> added = [];
        ProjectPropertiesViewModel changed = WithRemote(added);
        ProjectPropertiesViewModel reset = WithRemote(added);
        changed.KeepInPrivateRepository("https://host/owner/repo.git");
        reset.KeepInPrivateRepository("https://host/owner/repo.git");

        changed.ChooseHoldout(Path.Combine(Path.GetTempPath(), "x-holdout"));
        reset.ResetHoldout();

        Assert.That((changed.Apply(), reset.Apply()), Is.EqualTo((true, true)));
        Assert.That(added, Is.Empty, "the remote was meant for the folder in force when it was staged");
        Assert.That((changed.HasPendingRemote, reset.HasPendingRemote), Is.EqualTo((false, false)));
    }

    [Test]
    public void ProjectProperties_RefusesAnEmptyTitle_BeforeAddingTheRemote_SoCancelLeavesNothingWritten()
    {
        List<string> added = [];
        ProjectPropertiesViewModel properties = WithRemote(added);
        properties.KeepInPrivateRepository("https://host/owner/repo.git");
        properties.Title = "  ";

        Assert.That(properties.Apply(), Is.False);
        Assert.That((added.Count, properties.HasPendingRemote), Is.EqualTo((0, true)));
    }

    [Test]
    public void AQuestion_FocusesCancel_WhenItOpens()
    {
        QuestionWindow window = new("Accept spec?", "Accept the spec of auth?", "_Accept spec");

        Assert.That(FocusManager.GetFocusedElement(window), Is.SameAs(Buttons(window)[^1]), "W43: the default button, never the action");
    }

    [Test]
    public void TheInformationForm_HasOnlyOneButton_Close_WhichIsBothDefaultAndCancel()
    {
        QuestionWindow window = QuestionWindow.Information("Can't make a verifier for Auth yet", "Its holdout folder is not there.");
        List<Button> buttons = Buttons(window);

        Assert.That(buttons, Has.Count.EqualTo(1));
        Assert.That((buttons[0].Content, buttons[0].IsDefault, buttons[0].IsCancel), Is.EqualTo(("Close", true, true)));
        Assert.That(window.Title, Is.EqualTo("Can't make a verifier for Auth yet"), "the caller's title (A5)");
    }

    [Test]
    public void APrompt_NamesItsAction_AndHasNoAccessKeyOnOK()
    {
        TextPromptWindow plain = new("Rename", "New title", "Auth");
        TextPromptWindow named = new("Use a private repository", "Address", string.Empty) { ActionLabel = "_Add" };

        Assert.That(((Button)plain.FindName("OkButton")).Content, Is.EqualTo("OK"), "B10: no access key on OK");
        Assert.That(((Button)named.FindName("OkButton")).Content, Is.EqualTo("_Add"), "A-m2");
    }

    [Test]
    public void APromptWhoseValidatorRefuses_StaysOpen_AndShowsTheReasonUnderTheBox()
    {
        TextPromptWindow window = new("Use a private repository", "Address", "-x")
        {
            Validator = text => text.StartsWith('-') ? "Give the repository's address." : null,
        };
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        Click((Button)window.FindName("OkButton"));

        TextBlock refusal = (TextBlock)window.FindName("Refusal");
        Assert.That((closed, window.Accepted), Is.EqualTo((false, false)));
        Assert.That((refusal.Text, refusal.Visibility), Is.EqualTo(("Give the repository's address.", Visibility.Visible)));
    }

    [Test]
    public void APrompt_SaysWhyItsActionIsOff_AndItsBoxIsLabelledByItsLabel()
    {
        TextPromptWindow window = new("Rename", "New title", string.Empty);
        Button ok = (Button)window.FindName("OkButton");
        TextBox answer = (TextBox)window.FindName("Answer");

        Assert.That((ok.IsEnabled, ToolTipService.GetShowOnDisabled(ok)), Is.EqualTo((false, true)));
        Assert.That(ok.ToolTip as string, Is.Not.Empty, "A15, W13: a disabled command says why");
        Assert.That(System.Windows.Automation.AutomationProperties.GetLabeledBy(answer), Is.SameAs(window.FindName("Label")), "E12");
    }

    [Test]
    public void ProjectProperties_HoldsTheNewRemoteUntilOK_SoCancelAddsNothing()
    {
        List<string> added = [];
        ProjectPropertiesViewModel cancelled = WithRemote(added);
        ProjectPropertiesViewModel confirmed = WithRemote(added);

        cancelled.KeepInPrivateRepository("https://host/owner/cancelled.git");
        confirmed.KeepInPrivateRepository("https://host/owner/kept.git");

        Assert.That(added, Is.Empty, "nothing is added before OK (A2)");
        Assert.That(confirmed.Apply(), Is.True);
        Assert.That(added, Is.EqualTo(new[] { "https://host/owner/kept.git" }));
    }

    [Test]
    public void ProjectProperties_ShowsARefusedRemoteUnderTheHoldoutRow_AndStaysOpen()
    {
        ProjectPropertiesViewModel properties = new("p", "P", "#5E4BB0", "project.md") { AddRemote = _ => "git could not add it." };
        properties.KeepInPrivateRepository("https://host/owner/repo.git");

        Assert.That(properties.Apply(), Is.False);
        Assert.That((properties.HoldoutError, properties.HasHoldoutError), Is.EqualTo(("git could not add it.", true)), "A19");
    }

    [Test]
    public void ProjectProperties_ShowsARefusedHoldoutFolderInline()
    {
        ProjectPropertiesViewModel properties = new("p", "P", "#5E4BB0", "project.md");

        properties.ChooseHoldout("C:\\");

        Assert.That(properties.HoldoutError, Does.Contain("whole drive"), "A19: under HoldoutRow, not in a message box");
    }

    [Test]
    public void ProjectProperties_LeadChange_IsOffWithAReason_WhenThereAreNoWorkstreams()
    {
        ProjectPropertiesWindow window = new(new ProjectPropertiesViewModel("p", "P", "#5E4BB0", "project.md"));
        ((TabControl)window.FindName("Tabs")).SelectedItem = window.FindName("LeadTab");
        FrameworkElement content = (FrameworkElement)window.Content;
        content.Measure(new Size(520, 900));
        content.Arrange(new Rect(0, 0, 520, 900));
        content.UpdateLayout();
        Button change = (Button)window.FindName("LeadChange");

        Assert.That(change.IsEnabled, Is.False, "C11: no menu that holds only a message");
        Assert.That(change.ToolTip as string, Does.Contain("no workstreams"));
    }

    [Test]
    public void ProjectProperties_OKHasNoAccessKey_TheGrantIsAStatement_AndTheWindowHasAMinimumSize()
    {
        ProjectPropertiesWindow window = new(new ProjectPropertiesViewModel("p", "P", "#5E4BB0", "project.md"));
        Button ok = (Button)window.FindName("OkButton");

        Assert.That(ok.Content, Is.EqualTo("OK"), "B10");
        Assert.That(((CheckBox)window.FindName("BranchRulesBox")).Content, Is.EqualTo("Allow branch rules for this project"), "D15");
        Assert.That((window.MinWidth, window.MinHeight), Is.EqualTo((480d, 420d)), "D13");
    }

    [Test]
    public void AFlaggedStartWithNoOtherCards_OffersTwoButtons()
    {
        QuestionWindow window = new("Start paused or blocked work?", "The card you are starting is marked paused or blocked:",
            "_Clear marks and start", null);

        Assert.That(Buttons(window).Select(button => button.Content), Is.EqualTo(new object[] { "_Clear marks and start", "Cancel" }));
    }

    [Test]
    public void FlaggedStart_WithNoOtherCards_AsksWithoutYesOrNo()
    {
        string question = FlaggedStart.Question([Card("auth", paused: true)], others: 0);

        Assert.That(question, Does.StartWith("The card you are starting is marked paused or blocked:"));
        Assert.That(question, Does.Not.Contain("Yes").And.Not.Contain("No "), "A3: the buttons say what they do");
    }

    private static ProjectPropertiesViewModel WithRemote(List<string> added) =>
        new("p", "P", "#5E4BB0", "project.md")
        {
            AddRemote = address =>
            {
                added.Add(address);

                return null;
            },
        };

    private static Workstream Card(string key, bool paused = false) =>
        new(key, char.ToUpperInvariant(key[0]) + key[1..], "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New",
            false, false, false, null, [], null, DateTimeOffset.UnixEpoch)
        {
            IsPaused = paused,
        };

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    private static List<Button> Buttons(DependencyObject root)
    {
        List<Button> found = [];

        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is Button button)
            {
                found.Add(button);
            }

            if (child is DependencyObject inner)
            {
                found.AddRange(Buttons(inner));
            }
        }

        return found;
    }
}
