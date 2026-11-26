using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Colour;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// A project's dialog, as WPF lays it out: Project properties, the title read only, the colour chosen from the project
/// palette, and the project.md path (1.1d AC 40). Start's confirmation is gone: a project's Start starts its run state
/// with no list to tick (1.1e AC 9).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectDialogsTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static FrameworkElement LaidOut(Window window)
    {
        var content = (FrameworkElement)window.Content;

        content.Measure(new Size(520, 900));
        content.Arrange(new Rect(0, 0, 520, content.DesiredSize.Height));
        content.UpdateLayout();

        return content;
    }

    [Test]
    public void ProjectProperties_ShowsTheTitleToEdit_TheProjectPalette_AndTheProjectFileReadOnly()
    {
        var properties = new ProjectPropertiesViewModel(TheseusProject, "Theseus bootstrap", "#5E4BB0", @"C:\projects	heseus\project.md");
        var window = new ProjectPropertiesWindow(properties);
        LaidOut(window);
        var colours = (ListBox)window.FindName("ColourBox");

        Assert.That(((TextBox)window.FindName("TitleBox")).IsReadOnly, Is.False, "the title is edited here (1.1e AC 12a)");
        var file = (Theseus.App.Controls.SettingRow)window.FindName("FileRow");
        Assert.That(file.ToolTip as string, Does.Contain("Read only"));
        Assert.That((file.Value, file.Explanation), Is.EqualTo((@"C:\projects	heseus\project.md",
            "Read only: the file is where the project's folder is. Move the folder to move it.")), "shown, never edited here");
        Assert.That(colours.Items.Cast<ColourSwatchViewModel>().Select(swatch => swatch.Colour), Is.EqualTo(ProjectColourPalette.All));
        Assert.That(colours.SelectedValue, Is.EqualTo("#5E4BB0"));

        colours.SelectedValue = "#2F7D32";

        Assert.That(properties.ColourHex, Is.EqualTo("#2F7D32"));
    }

    [Test]
    public void AColourAnotherProjectHolds_IsMarked_AndNamesIt_ButCanStillBeChosen()
    {
        var properties = new ProjectPropertiesViewModel(TheseusProject, "Theseus bootstrap", "#5E4BB0", "project.md",
            new Dictionary<string, string> { ["BuildYourMarket MCP server"] = "#2F7D32" });
        var window = new ProjectPropertiesWindow(properties);
        var laidOut = LaidOut(window);
        var taken = properties.Colours.Single(swatch => swatch.Colour == "#2F7D32");
        var marks = Descendants<FrameworkElement>(laidOut).Where(mark => mark.Name == "TakenMark").ToList();

        Assert.That((taken.IsTaken, taken.ToolTip), Is.EqualTo((true, "#2F7D32, used by BuildYourMarket MCP server")));
        Assert.That(properties.Colours.Count(swatch => swatch.IsTaken), Is.EqualTo(1));
        Assert.That(marks.Count(mark => mark.Visibility == Visibility.Visible), Is.EqualTo(1));

        ((ListBox)window.FindName("ColourBox")).SelectedValue = "#2F7D32";

        Assert.That(properties.ColourHex, Is.EqualTo("#2F7D32"));
    }

    [Test]
    public void AColourFromOutsideThePalette_IsOfferedFirst_SoItIsNotLostByOpeningTheDialog()
    {
        var properties = new ProjectPropertiesViewModel("p", "P", "#123456", "project.md");

        Assert.That(properties.Colours[0].Colour, Is.EqualTo("#123456"));
        Assert.That(properties.Colours.Skip(1).Select(swatch => swatch.Colour), Is.EqualTo(ProjectColourPalette.All));
    }

    [Test]
    public void ProjectProperties_HasATabPerSection_AndOpensAtHoldouts_WhenSetTheHoldoutFolderAsks()
    {
        var properties = new ProjectPropertiesViewModel(TheseusProject, "Theseus", "#5E4BB0", "project.md")
        {
            OpenAt = ProjectPropertiesViewModel.HoldoutsTab,
            Leads = [new("git-22", "Theseus (git-22)")],
            Lead = "git-22",
            Holdout = new Theseus.Core.Links.HoldoutLocation(Path.GetTempPath(), false) { Source = Theseus.Core.Links.HoldoutSource.Recorded },
        };
        var window = new ProjectPropertiesWindow(properties);
        var tabs = (TabControl)window.FindName("Tabs");

        Assert.That(tabs.Items.Cast<TabItem>().Select(tab => tab.Header), Is.EqualTo(new[] { "General", "Lead and branches", "Holdouts" }));
        Assert.That(tabs.SelectedItem, Is.SameAs(window.FindName("HoldoutsTab")));

        var holdout = (Theseus.App.Controls.SettingRow)window.FindName("HoldoutRow");
        var lead = (Theseus.App.Controls.SettingRow)window.FindName("LeadRow");
        var rules = (Theseus.App.Controls.SettingRow)window.FindName("BranchRulesRow");
        LaidOut(window);

        Assert.That(holdout.Value, Is.EqualTo(Path.GetTempPath()), "a folder that is there");
        Assert.That(holdout.Explanation, Does.StartWith("Recorded in project.md, beside the repo."), "it says how it was found");
        Assert.That(holdout.Explanation, Does.Contain("Looked for in this order"));
        Assert.That(lead.Value, Is.EqualTo("Theseus (git-22)"), "the lead as text, changed through Change…");
        Assert.That(rules.Value, Is.EqualTo("Cannot be granted"), "no primary branch is named");
    }

    [Test]
    public void Options_HasATabPerSection_AndEverySettingSaysItsValueAndWhatItDoes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "theseus-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var settings = new SettingsViewModel(new Theseus.Core.Settings.SettingsStore(Path.Combine(folder, "settings.json")), () => { })
            {
                Guards = [new GuardViewModel(new Theseus.Core.Guards.GuardInstallation(Theseus.Core.Guards.GuardCatalog.Holdout, Path.Combine(folder, "claude"), folder))],
                AutoMode = new AutoModeViewModel(new Theseus.Core.Settings.SettingsStore(Path.Combine(folder, "settings.json")),
                    new Theseus.Core.Guards.AutoModeEnvironment(Path.Combine(folder, "claude"))),
                FolderTrust = new FolderTrustViewModel(
                    new Theseus.Core.Trust.ClaudeFolderTrust(Path.Combine(folder, ".claude.json"), Path.Combine(folder, "undo.json")), () => []),
                Tags = null,
            };
            settings.OpenCommand.Execute(null);
            settings.HoldoutEntries = [new("theseus", folder, Theseus.Core.Links.HoldoutSource.Recorded)];
            var window = new SettingsWindow { DataContext = settings };
            var tabs = (TabControl)window.FindName("Tabs");

            Assert.That(tabs.Items.Cast<TabItem>().Select(tab => tab.Header),
                Is.EqualTo(new[] { "General", "Board", "Folders", "Guards", "Tags", "Prompts", "Advanced" }));

            var rows = new List<Theseus.App.Controls.SettingRow>();

            foreach (TabItem tab in tabs.Items)
            {
                tabs.SelectedItem = tab;
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(680, 2000));
                content.Arrange(new Rect(0, 0, 680, 2000));
                content.UpdateLayout();

                Assert.That(Descendants<Expander>(content), Is.Empty, $"{tab.Header}: tabs, not a column of expanders");
                Assert.That(Descendants<TextBox>(content), Is.Empty, $"{tab.Header}: no text box; a value is changed through a dialog");
                rows.AddRange(Descendants<Theseus.App.Controls.SettingRow>(content));
            }

            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(17));
            Assert.That(rows.Where(row => string.IsNullOrWhiteSpace(row.Label) || string.IsNullOrWhiteSpace(row.Value) || string.IsNullOrWhiteSpace(row.Explanation))
                .Select(row => row.Label), Is.Empty, "every setting says its value and what it does now");
            Assert.That(rows.Single(row => row.Label == "theseus").Value, Is.EqualTo(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void NoOptionReachesEveryProject()
    {
        var options = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "SettingsWindow.xaml"));

        Assert.That(options, Does.Not.Contain("for every project"), "a project's holdout folder is its own (options-made-clear item 1)");
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
