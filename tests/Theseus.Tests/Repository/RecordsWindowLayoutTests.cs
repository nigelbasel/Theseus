using System.Windows;
using System.Windows.Controls;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// The Records window as WPF lays it out: its commands in one bar at the top, Preview and Source a selector bar, Open in
/// editor and Open folder toolbar buttons, no button at the bottom and no link beside it (walkthrough row 42).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class RecordsWindowLayoutTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheCommands_AreOneBarAtTheTop_AboveTheDocument()
    {
        var model = new RecordsViewModel(Named(BymPieces(_folder), "bym-mcp-identity"), "Plan", _ => { });
        var window = new RecordsWindow(model);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(1000, 720));
        content.Arrange(new Rect(0, 0, 1000, 720));
        content.UpdateLayout();
        double Top(string name) => ((FrameworkElement)window.FindName(name)).TranslatePoint(new Point(0, 0), content).Y;
        var entries = Top("Entries");

        Assert.Multiple(() =>
        {
            foreach (var name in new[] { "PreviewTab", "SourceTab", "OpenInEditor", "OpenFolder" })
            {
                Assert.That(Top(name), Is.LessThan(entries), $"{name} is above the document");
            }

            Assert.That(window.FindName("PreviewTab"), Is.InstanceOf<RadioButton>(), "a selector bar, not links");
            Assert.That(((RadioButton)window.FindName("PreviewTab")).IsChecked, Is.True);
            Assert.That(((Button)window.FindName("OpenFolder")).Style, Is.SameAs(((Button)window.FindName("OpenInEditor")).Style), "both toolbar buttons");
        });

        model.SourceCommand.Execute(null);

        Assert.That(((RadioButton)window.FindName("SourceTab")).IsChecked, Is.True);
    }
}
