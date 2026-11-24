using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Theseus.App.Controls;
using Theseus.App.Markdown;
using Theseus.Core.Markdown;

namespace Theseus.Tests.Markdown;

/// <summary>
/// The Markdown box (1.1e AC 22, 25): the toolbar and the keys on the real control, the text kept exactly as typed, and
/// the native preview of each element (AC 23).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class MarkdownBoxTests
{
    [Test]
    public void TheToolbar_HasEachButton_InOrder_EachNamed()
    {
        var box = new MarkdownBox();

        Assert.That(box.Tools.Select(tool => System.Windows.Automation.AutomationProperties.GetName(tool)), Is.EqualTo(new[]
        {
            "Bold (Ctrl+B)", "Italic (Ctrl+I)", "Heading", "Bulleted list", "Numbered list", "Check box",
            "Code: inline on one line, a block over several", "Link (Ctrl+K)",
        }));
    }

    [Test]
    public void AButton_ChangesTheTextAsTyped_AndUndoTakesItBack()
    {
        // A text box keeps Undo only once it is in a window, as it always is in the app.
        var box = new MarkdownBox { Text = "make this bold" };
        var window = new Window
        {
            Content = box, Left = -20000, Top = -20000, Width = 400, Height = 300, ShowInTaskbar = false, ShowActivated = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();

        try
        {
            box.Editor.Select(5, 4);

            box.Format(MarkdownAction.Bold);

            Assert.That((box.Text, box.Editor.SelectedText), Is.EqualTo(("make **this** bold", "this")));

            box.Editor.Undo();

            Assert.That(box.Text, Is.EqualTo("make this bold"));
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void ALineButton_InTheRealBox_KeepsTheCaretOnItsLine()
    {
        var box = new MarkdownBox();
        box.Editor.Text = "one\r\ntwo";
        box.Editor.Select(1, 0);

        box.Format(MarkdownAction.Heading);
        box.Editor.SelectedText = "X";

        Assert.That(box.Text, Is.EqualTo("## oneX\r\ntwo"));
    }

    [Test]
    public void ReadOnly_SaysItsOwnReason()
    {
        var box = new MarkdownBox { IsReadOnly = true, ReadOnlyReason = "Sent once, already" };

        Assert.That(box.Tools.Select(tool => tool.ToolTip).Distinct(), Is.EqualTo(new object[] { "Sent once, already" }));
    }

    [Test]
    public void ThePreview_ReadsInsideBoldAgain()
    {
        var paragraph = (Paragraph)MarkdownRenderer.Render("**run `dotnet test` and [see](https://example.com)**").Blocks.First();
        var bold = (Bold)paragraph.Inlines.First();

        Assert.That(bold.Inlines.Select(inline => inline.GetType().Name), Is.EqualTo(new[] { "Run", "Run", "Run", "Hyperlink" }));
    }

    [TestCase(Key.B, "**bold text**")]
    [TestCase(Key.I, "*italic text*")]
    [TestCase(Key.K, "[link text](https://)")]
    public void CtrlBIAndK_AreTheirButtons(Key key, string text)
    {
        var box = new MarkdownBox();

        Assert.That((box.HandleShortcut(key, ModifierKeys.Control), box.Text), Is.EqualTo((true, text)));
        Assert.That(box.HandleShortcut(key, ModifierKeys.Control | ModifierKeys.Shift), Is.False, "only Ctrl alone");
    }

    [Test]
    public void ReadOnly_TheToolbarIsOff_SaysWhy_AndTheKeysDoNothing()
    {
        var box = new MarkdownBox { Text = "kept", IsReadOnly = true };

        Assert.That(box.Tools.All(tool => !tool.IsEnabled && (string?)tool.ToolTip == "This text cannot be changed now"), Is.True);
        Assert.That((box.HandleShortcut(Key.B, ModifierKeys.Control), box.Text), Is.EqualTo((false, "kept")));
    }

    [Test]
    public void Preview_DrawsTheText_AndFollowsIt_WriteShowsTheBoxAgain()
    {
        var box = new MarkdownBox { Text = "# Heading" };

        box.IsPreviewing = true;

        Assert.That((box.Editor.Visibility, TextOf(box.PreviewDocument!)), Is.EqualTo((Visibility.Collapsed, "Heading")));

        box.Text = "changed";

        Assert.That(TextOf(box.PreviewDocument!), Is.EqualTo("changed"));

        box.IsPreviewing = false;

        Assert.That(box.Editor.Visibility, Is.EqualTo(Visibility.Visible));
    }

    [Test]
    public void ThePreview_DrawsEachElement_Natively()
    {
        var document = MarkdownRenderer.Render("""
            # Big
            Some **bold**, *it*, `code` and [link](https://example.com).

            - one
            1. first
            - [x] done

            > quote

            ```
            code block
            ```

            | A | B |
            |---|---|
            | 1 | 2 |

            ---
            """);
        var blocks = document.Blocks.ToList();

        Assert.That(blocks.Select(block => block.GetType().Name), Is.EqualTo(new[]
        {
            "Paragraph", "Paragraph", "List", "List", "List", "Paragraph", "Paragraph", "Table", "Paragraph",
        }));
        Assert.That(((Paragraph)blocks[0]).FontSize, Is.EqualTo(20), "Subtitle on the type ramp (baseline W18)");
        var inlines = ((Paragraph)blocks[1]).Inlines.ToList();
        Assert.That(inlines.Select(inline => inline.GetType().Name), Is.EqualTo(new[] { "Run", "Bold", "Run", "Italic", "Run", "Run", "Run", "Hyperlink", "Run" }));
        Assert.That(((Hyperlink)inlines[7]).NavigateUri, Is.EqualTo(new Uri("https://example.com")));
        Assert.That(((List)blocks[2]).MarkerStyle, Is.EqualTo(TextMarkerStyle.Disc));
        Assert.That(((List)blocks[3]).MarkerStyle, Is.EqualTo(TextMarkerStyle.Decimal));
        Assert.That(TextOf(blocks[4]), Is.EqualTo("☑ done"));
        Assert.That(((Paragraph)blocks[5]).BorderThickness, Is.EqualTo(new Thickness(3, 0, 0, 0)), "a quote's bar");
        Assert.That(((Paragraph)blocks[6]).FontFamily, Is.EqualTo(MarkdownRenderer.Mono));
        Assert.That(((Table)blocks[7]).RowGroups[0].Rows.Count, Is.EqualTo(2));
    }

    private static string TextOf(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text.Trim();

    private static string TextOf(Block block) =>
        new TextRange(block.ContentStart, block.ContentEnd).Text.Trim();
}
