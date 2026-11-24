using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Theseus.App;
using Theseus.App.Controls;

namespace Theseus.Tests.Windows;

/// <summary>
/// What one secondary window needs that the others do not (batch WB5): the spec's views as a selector bar (WB1's hand-on,
/// baseline B-m4, D7, E8; W15), the day report's F5 and the two titles that match their commands (B18, A30), the
/// instructions' plugin items as a toggle switch (D3, W40), a checklist row whose detail sits outside its check box (D15),
/// the links About and the day report offer (C22, D16, W12, W53, W54), and the reasons a disabled commit gives (A15, W13).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SecondaryWindowSpecificsTests
{
    [Test]
    public void TheSpecWindow_SwitchesItsViews_WithASelectorBar()
    {
        using OpenedWindow opened = OpenedWindow.Of("Spec");
        Style item = (Style)opened.Window.FindResource("SelectorBarItem");
        RadioButton preview = opened.Named<RadioButton>("PreviewTab");
        RadioButton source = opened.Named<RadioButton>("SourceTab");
        SpecWindow window = (SpecWindow)opened.Window;

        Assert.Multiple(() =>
        {
            Assert.That(preview.Style, Is.SameAs(item));
            Assert.That(source.Style, Is.SameAs(item));
            Assert.That(SelectorBar.GetSelectsOnFocus(preview) && SelectorBar.GetSelectsOnFocus(source), Is.True, "the arrows select");
            Assert.That(preview.IsChecked, Is.True, "it opens on Preview");
        });

        window.Model.SourceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(source.IsChecked, Is.True);
            Assert.That(preview.IsChecked, Is.False);
            Assert.That(opened.Named<UIElement>("SourceView").Visibility, Is.EqualTo(Visibility.Visible));
        });
    }

    [Test]
    public void TheSpecWindow_OpensItsFileInTheEditor_WithALink()
    {
        using OpenedWindow opened = OpenedWindow.Of("Spec");

        Assert.That(opened.Named<Button>("EditorButton").Style, Is.SameAs(opened.Window.FindResource("LinkButton")), "W12");
    }

    [Test]
    public void TheDayReport_RefreshesOnF5_AndIsCalledDayReport()
    {
        using OpenedWindow opened = OpenedWindow.Of("DayReport");
        KeyBinding? f5 = opened.Window.InputBindings.OfType<KeyBinding>().SingleOrDefault(binding => binding.Key == Key.F5);

        Assert.Multiple(() =>
        {
            Assert.That(f5, Is.Not.Null, "F5 refreshes (B18, W27)");
            Assert.That(opened.Window.Title, Is.EqualTo("Day report"), "the command's name (A30)");
            Assert.That(opened.Named<Button>("OpenFolderButton").Style, Is.SameAs(opened.Window.FindResource("LinkButton")), "a link (W12)");
            Assert.That((string)opened.Named<Button>("RefreshButton").ToolTip, Does.Contain("(F5)"));
        });
    }

    [Test]
    public void TheArrangeWindow_IsCalledArrangeWindows()
    {
        using OpenedWindow opened = OpenedWindow.Of("Arrange");

        Assert.That(opened.Window.Title, Is.EqualTo("Arrange windows"), "A30");
    }

    [Test]
    public void ThePluginItems_AreAToggleSwitch()
    {
        using OpenedWindow opened = OpenedWindow.Of("Instructions");
        ToggleButton plugins = opened.Named<ToggleButton>("PluginItems");

        Assert.That(plugins.Style, Is.SameAs(opened.Window.FindResource("ToggleSwitch")), "it applies at once (D3, W40)");
    }

    [Test]
    public void AChecklistRow_KeepsItsDetailOutsideTheCheckBox()
    {
        using OpenedWindow opened = OpenedWindow.Of("Checklist");
        ItemsControl rows = opened.Named<ItemsControl>("Rows");
        DependencyObject first = rows.ItemContainerGenerator.ContainerFromIndex(0);
        CheckBox box = DialogHost.Descendants(first).OfType<CheckBox>().Single();
        List<string> inBox = [.. DialogHost.Descendants(box).OfType<TextBlock>().Select(text => text.Text)];
        List<string> inRow = [.. DialogHost.Descendants(first).OfType<TextBlock>().Select(text => text.Text)];

        Assert.Multiple(() =>
        {
            Assert.That(inBox, Does.Not.Contain("Done 2 days ago."), "D15: the check box says only what is ticked");
            Assert.That(inRow, Does.Contain("Done 2 days ago."));
            Assert.That(inRow, Does.Contain("Its session is still open."));
        });
    }

    [TestCase("AddObjective", "AddButton")]
    [TestCase("AddToProject", "AddButton")]
    [TestCase("NewProject", "CreateButton")]
    public void ADisabledCommit_SaysWhy(string name, string button)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        Button commit = opened.Named<Button>(button);

        if (name == "NewProject")
        {
            opened.Named<TextBox>("NameBox").Text = "Not A Slug";
        }

        Assert.Multiple(() =>
        {
            Assert.That(commit.IsEnabled, Is.False);
            Assert.That(ToolTipService.GetShowOnDisabled(commit), Is.True, "A15, W13");
            Assert.That(commit.ToolTip as string, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void TheNewProjectHint_TurnsCritical_WhileTheNameIsNotValid()
    {
        using OpenedWindow opened = OpenedWindow.Of("NewProject");
        TextBlock hint = opened.Named<TextBlock>("NameHint");
        object normal = hint.Foreground;

        opened.Named<TextBox>("NameBox").Text = "Not A Slug";

        Assert.That(hint.Foreground, Is.SameAs(opened.Window.FindResource("CriticalTextBrush")), "A15");

        opened.Named<TextBox>("NameBox").Text = "my-project";

        Assert.That(hint.Foreground, Is.SameAs(normal));
    }

    [Test]
    public void About_ShowsTheIcon_AndSaysWhereEachLinkGoes()
    {
        using OpenedWindow opened = OpenedWindow.Of("About");
        List<System.Windows.Documents.Hyperlink> links = [.. Inlines(opened.Named<TextBlock>("Links"))];

        Assert.Multiple(() =>
        {
            Assert.That(opened.Named<Image>("AppIcon").Source, Is.Not.Null, "D16, W53");
            Assert.That(links.Select(link => link.NavigateUri.AbsoluteUri), Does.Contain("https://github.com/nigelbasel/Theseus/issues"), "Report a problem (W53)");
            Assert.That(links.Where(link => link.ToolTip as string != link.NavigateUri.AbsoluteUri).Select(link => link.NavigateUri.AbsoluteUri), Is.Empty,
                "each link's address is its tooltip (C22, W54)");
        });
    }

    private static IEnumerable<System.Windows.Documents.Hyperlink> Inlines(TextBlock text) =>
        text.Inlines.OfType<System.Windows.Documents.Hyperlink>();
}
