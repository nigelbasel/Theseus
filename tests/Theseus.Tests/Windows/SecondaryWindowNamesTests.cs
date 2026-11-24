using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Theseus.Tests.Windows;

/// <summary>
/// What a screen reader hears in the secondary windows, and the access keys they offer (batch WB5; baseline A13, B9, C6,
/// E9, E12, E-m2; decisions W26, W50; checklist G34, G81, G84, G92, G167, G168): every field and every button has a name,
/// no Markdown editor is called "Markdown text", and no access key is used twice in one window.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SecondaryWindowNamesTests
{
    [TestCaseSource(typeof(SecondaryWindowNames), nameof(SecondaryWindowNames.Every))]
    public void EveryFieldAndButton_HasAName_AndNoEditorIsCalledMarkdownText(string name)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        List<Control> controls = [.. Everything(opened).OfType<Control>().Where(control => control is TextBox or ComboBox or ButtonBase)];

        Assert.That(controls, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (Control control in controls)
            {
                string spoken = UIElementAutomationPeer.CreatePeerForElement(control).GetName();

                Assert.That(spoken, Is.Not.Empty, $"{Describe(control)} has a name (labelled by {Label(control)})");
                Assert.That(spoken, Is.Not.EqualTo("Markdown text"), $"{Describe(control)} is named from its label (E12)");
            }
        });
    }

    [TestCaseSource(typeof(SecondaryWindowNames), nameof(SecondaryWindowNames.Every))]
    public void NoAccessKey_IsUsedTwice(string name)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        List<char> keys = [.. opened.Logical().OfType<ContentControl>().Select(control => control.Content as string)
            .Concat(opened.Logical().OfType<AccessText>().Select(text => text.Text))
            .Select(Key).Where(key => key is not null).Select(key => key!.Value)];

        Assert.That(keys.GroupBy(key => key).Where(group => group.Count() > 1).Select(group => group.Key), Is.Empty,
            $"each key once in the window (W26): {string.Join(", ", keys)}");
    }

    [TestCaseSource(typeof(SecondaryWindowNames), nameof(SecondaryWindowNames.Every))]
    public void EveryLabelsAccessKey_ReachesAFieldThatTakesTheFocus(string name)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);

        Assert.Multiple(() =>
        {
            foreach (Label label in opened.Logical().OfType<Label>().Where(label => label.Target is not null))
            {
                Assert.That(label.Target.Focusable, Is.True, $"{label.Content}: Alt with its key moves to its field (W26)");
            }
        });
    }

    [TestCase("About", "CheckButton", 'C')]
    [TestCase("Arrange", "ArrangeButton", 'A')]
    [TestCase("Arrange", "PutBackButton", 'U')]
    [TestCase("Arrange", "LookAgainButton", 'L')]
    [TestCase("DayReport", "RefreshButton", 'R')]
    [TestCase("NewWorkstream", "CreateButton", 'C')]
    [TestCase("Proposal", "ConfirmRejectButton", 'P')]
    [TestCase("Spec", "AcceptButton", 'A')]
    public void ACommonButton_HasItsAccessKey(string window, string button, char key)
    {
        using OpenedWindow opened = OpenedWindow.Of(window);

        Assert.That(Key(opened.Named<Button>(button).Content as string), Is.EqualTo(key), "A13, B9");
    }

    // The window's own controls, those of a part that is collapsed for now included; not the insides of templates.
    private static IEnumerable<DependencyObject> Everything(OpenedWindow opened) =>
        opened.Logical().Where(element => element is UIElement { Visibility: Visibility.Visible });

    private static char? Key(string? text)
    {
        if (text is null)
        {
            return null;
        }

        for (int at = 0; at < text.Length - 1; at++)
        {
            if (text[at] == '_')
            {
                if (text[at + 1] == '_')
                {
                    at++;
                    continue;
                }

                return char.ToUpperInvariant(text[at + 1]);
            }
        }

        return null;
    }

    // What a field is labelled by, for a failure's message: which element, and what its own peer says.
    private static string Label(Control control) =>
        System.Windows.Automation.AutomationProperties.GetLabeledBy(control) is { } label
            ? $"{label.GetType().Name} {(label as FrameworkElement)?.Name} saying \"{UIElementAutomationPeer.CreatePeerForElement(label)?.GetName()}\""
            : "nothing";

    private static string Describe(Control control) =>
        $"{control.GetType().Name} {(control.Name.Length > 0 ? control.Name : "(unnamed)")}";
}
