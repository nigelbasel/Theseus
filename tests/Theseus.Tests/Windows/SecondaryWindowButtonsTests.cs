using System.Windows;
using System.Windows.Controls;

namespace Theseus.Tests.Windows;

/// <summary>
/// The footers of the secondary windows (batch WB5; baseline A4, A7, A12, A16, A17, B10, B13, B14; decisions W9, W11, W12,
/// W45; checklist G12, G13, G14, G24, G32, G69, G70, G83): one cancel button at most; Enter on the commit button, or on
/// Cancel where the commit must not fire from Enter, or on Close where nothing is committed; one accented commit button,
/// and none where there is no commit; no access key on OK, Cancel or Close.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SecondaryWindowButtonsTests
{
    // Window, its IsDefault button (null for none), its IsCancel button, its accented button (null for none).
    private static readonly object?[][] Footers =
    [
        ["About", "CloseButton", "CloseButton", null],
        ["AddObjective", "AddButton", "CancelButton", "AddButton"],
        ["AddToProject", "AddButton", "CancelButton", "AddButton"],
        ["Arrange", "ArrangeButton", "CloseButton", "ArrangeButton"],
        ["Checklist", "CancelButton", "CancelButton", null],
        ["DayReport", "CloseButton", "CloseButton", null],
        ["FirstRun", "ForwardButton", "SkipButton", "ForwardButton"],
        ["Instructions", "CloseButton", "CloseButton", null],
        ["NewProject", "CreateButton", "CancelButton", "CreateButton"],
        ["NewWorkstream", null, "CancelButton", "CreateButton"],
        ["PromptEdit", "OkButton", "CancelButton", "OkButton"],
        ["Proposal", "ApproveButton", "CloseButton", "ApproveButton"],
        ["Records", "CloseButton", "CloseButton", null],
        ["Spec", "AcceptButton", "CloseButton", "AcceptButton"],
        ["Waits", "CloseButton", "CloseButton", null],
    ];

    [TestCaseSource(nameof(Footers))]
    public void TheFooter_HasOneCancel_TheRightDefault_AndOneAccentAtMost(string name, string? isDefault, string isCancel, string? accent)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        IReadOnlyList<Button> buttons = opened.Buttons();
        Style accentStyle = (Style)opened.Window.FindResource("AccentButton");

        Assert.Multiple(() =>
        {
            Assert.That(buttons.Where(button => button.IsCancel).Select(button => button.Name), Is.EqualTo(new[] { isCancel }), "one IsCancel button");
            Assert.That(buttons.Where(button => button.IsDefault).Select(button => button.Name),
                Is.EqualTo(isDefault is null ? Array.Empty<string>() : new[] { isDefault }), "the IsDefault button");
            Assert.That(buttons.Where(button => ReferenceEquals(button.Style, accentStyle)).Select(button => button.Name),
                Is.EqualTo(accent is null ? Array.Empty<string>() : new[] { accent }), "the accented button");
        });
    }

    [TestCaseSource(typeof(SecondaryWindowNames), nameof(SecondaryWindowNames.Every))]
    public void EveryFooterButton_IsAtLeast120Wide_AndOkCancelAndCloseHaveNoAccessKey(string name)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        Panel footer = opened.Named<Panel>("Footer");
        Style accentStyle = (Style)opened.Window.FindResource("AccentButton");
        Style dialogStyle = (Style)opened.Window.FindResource("DialogButton");
        List<Button> buttons = [.. LogicalTreeHelper.GetChildren(footer).OfType<DependencyObject>().SelectMany(Self).OfType<Button>()];

        Assert.Multiple(() =>
        {
            foreach (Button button in buttons.Where(button => !IsLink(button, opened.Window)))
            {
                Assert.That(ReferenceEquals(button.Style, accentStyle) || ReferenceEquals(button.Style, dialogStyle), Is.True,
                    $"{button.Name} is a DialogButton or the AccentButton (W11)");
                Assert.That(button.MinWidth, Is.GreaterThanOrEqualTo(120), $"{button.Name} is at least 120 wide");
            }

            Assert.That(buttons.Select(button => button.Content as string ?? string.Empty)
                .Where(text => text.Contains('_') && text.Replace("_", string.Empty, StringComparison.Ordinal) is "OK" or "Cancel" or "Close"),
                Is.Empty, "no access key on OK, Cancel or Close (B10)");
        });
    }

    [Test]
    public void TheProposalWindow_IsClosed_NotCancelled()
    {
        using OpenedWindow opened = OpenedWindow.Of("Proposal");

        Assert.That(opened.Named<Button>("CloseButton").Content, Is.EqualTo("Close"), "it is modeless: nothing is cancelled (A7, W17)");
    }

    [Test]
    public void TheNewWorkstreamWindow_KeepsCtrlEnterForCreate()
    {
        using OpenedWindow opened = OpenedWindow.Of("NewWorkstream");

        Assert.That(opened.Window.InputBindings.OfType<System.Windows.Input.KeyBinding>()
            .Any(binding => binding.Key == System.Windows.Input.Key.Enter && binding.Modifiers == System.Windows.Input.ModifierKeys.Control),
            Is.True, "W45, a recorded deviation");
    }

    private static IEnumerable<DependencyObject> Self(DependencyObject element) =>
        [element, .. LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>().SelectMany(Self)];

    private static bool IsLink(Button button, Window window) => ReferenceEquals(button.Style, window.FindResource("LinkButton"));
}
