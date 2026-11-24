using System.Windows.Controls;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// Project properties' lead chooser is one choice of a set (baseline C12, C-m3; decision W32; checklist G54): each card is
/// a radio item, the lead marked by its bullet, and a title keeps its underscores rather than losing one to an access key.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class LeadMenuTests
{
    [Test]
    public void TheLeadsAreRadioItems_TheCurrentOneMarked_TheirTitlesEscaped()
    {
        string? chosen = null;
        IReadOnlyList<MenuItem> items = LeadMenu.Items([new("a", "auth_spike"), new("b", "Build")], "b", key => chosen = key);

        Assert.That(items.Select(item => item.Header), Is.EqualTo(new[] { "auth__spike", "Build" }), "C-m3: one helper escapes bound names");
        Assert.That(items.Select(item => item.IsChecked), Is.EqualTo(new[] { false, true }));
        Assert.That(items.Select(item => item.IsCheckable), Is.All.False, "C12: a radio item, chosen by a click, not toggled");

        items[0].RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));

        Assert.That(chosen, Is.EqualTo("a"));
    }

    [Test]
    public void TheLeadsExplanation_NamesTheCommand_WithoutAGlyph() =>
        Assert.That(File.ReadAllText(Theseus.Tests.Conventions.LooseXaml.SourceFile("ProjectPropertiesWindow.xaml")), Does.Not.Contain("⌖"),
            "C2: the command is Focus lead; the glyph is the project view's, not a word");
}
