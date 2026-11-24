using System.Windows;
using System.Windows.Controls;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using Theseus.Tests.Windows;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// The whole UI Automation tree of the main window, the project view and every dialog, as a screen reader gets it
/// (baseline WB9, W51, E16; checklist G165, G167, G170, G172). Each test fails on a Button, list or tree item, list,
/// tree, check box, radio button, link or tab with no name, and on any name that is a .NET type name or a record's
/// ToString, such as "ChainNodeViewModel { Key = … }". Built off screen; no test moves keyboard focus.
/// </summary>
[TestFixture]
public class AutomationTreeTests
{
    [Test]
    [NonParallelizable]
    public void TheMainWindow_WithItsPanelsOpen_AnnouncesNothingWrong() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                ChainNodeViewModel root = Chain("alpha", "Alpha");
                root.Children.Add(Chain("beta", "Beta"));
                shell.Chains.Roots.Add(root);
                shell.Chains.IsOpen = true;
                shell.IsSessionsPanelOpen = true;
                shell.Drawer.Show(shell.Board.Cards.Single(card => card.Key == "alpha"));

                // The fake's panel flags do not notify, so the window is bound again to read them.
                window.DataContext = null;
                window.DataContext = shell;
                Relayout(window);

                IReadOnlyList<(System.Windows.Automation.Peers.AutomationControlType Type, string Name, string Where)> peers =
                    AutomationTree.Walk((UIElement)window.Content);

                Assert.That(peers.Count(peer => peer.Type == System.Windows.Automation.Peers.AutomationControlType.TreeItem), Is.EqualTo(2),
                    "the walk reached both Chains rows");
                AssertNothingWrong(AutomationTree.Problems((UIElement)window.Content));
            },
            board => board.With("alpha").With("beta", column: "In progress").Tag("alpha", "cache"));

    [Test]
    [NonParallelizable]
    public void TheMainWindow_WithTheLocksPanelOpen_AnnouncesNothingWrong() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                shell.Locks.IsOpen = true;
                window.DataContext = null;
                window.DataContext = shell;
                Relayout(window);

                AssertNothingWrong(AutomationTree.Problems((UIElement)window.Content));
            },
            board => board.With("alpha"));

    [TestCase("Theseus", false)]
    [TestCase("Theseus", true)]
    [TestCase("BuildYourMarket", false)]
    [TestCase("BuildYourMarket", true)]
    [Apartment(ApartmentState.STA)]
    public void TheProjectView_Unfolded_AnnouncesNothingWrong(string project, bool byObjectives)
    {
        using TestProjectsFolder folder = TestProjectsFolder.Create();
        ProjectViews views = new(folder);
        ProjectViewModel view = project == "Theseus" ? views.Theseus() : views.BuildYourMarket();
        view.IsGroupedByObjectives = byObjectives;
        view.ExpandAllCommand.Execute(null);
        ProjectView control = new() { DataContext = view, Width = 1400, Height = 900 };
        control.Measure(new Size(1400, 900));
        control.Arrange(new Rect(0, 0, 1400, 900));
        control.UpdateLayout();

        Assert.That(AutomationTree.Walk(control).Count(peer => peer.Type == System.Windows.Automation.Peers.AutomationControlType.DataItem),
            Is.GreaterThan(0), "the walk reached the strip's items");
        AssertNothingWrong(AutomationTree.Problems(control));
    }

    [TestCaseSource(typeof(SecondaryWindowNames), nameof(SecondaryWindowNames.Every))]
    [Apartment(ApartmentState.STA)]
    public void ASecondaryWindow_AnnouncesNothingWrong(string name)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);

        AssertNothingWrong(AutomationTree.Problems(opened.Window));
    }

    [TestCaseSource(nameof(OtherDialogs))]
    [Apartment(ApartmentState.STA)]
    public void ADialog_AnnouncesNothingWrong(string name)
    {
        AssertNothingWrong(ProblemsOf(Build(name)));
    }

    // Setup's own resources are StaticResources from App.xaml, which resolve only where the Application holds them.
    [Test]
    [NonParallelizable]
    public void TheSetupDialog_AnnouncesNothingWrong() =>
        MainWindowFixture.Run(
            (_, shell) =>
            {
                shell.Drawer.Show(shell.Board.Cards.Single(card => card.Key == "alpha"));
                AssertNothingWrong(ProblemsOf(new SetupWindow { DataContext = shell }));
            },
            board => board.With("alpha"));

    private static IEnumerable<string> OtherDialogs() => ["Question", "Information", "TextPrompt", "ProjectProperties", "Settings"];

    // Every tab of a tabbed dialog in turn, since a tab's content is in the tree only while it is selected.
    private static List<string> ProblemsOf(Window built)
    {
        Window window = DialogHost.Open(built);

        try
        {
            if (window.FindName("Tabs") is not TabControl tabs)
            {
                return [.. AutomationTree.Problems(window)];
            }

            List<string> problems = [];

            foreach (TabItem tab in tabs.Items)
            {
                tabs.SelectedItem = tab;
                DialogHost.Pump();
                problems.AddRange(AutomationTree.Problems(window).Select(problem => $"{tab.Header}: {problem}"));
            }

            return [.. problems.Distinct()];
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertNothingWrong(IReadOnlyList<string> problems) =>
        Assert.That(problems, Is.Empty, Environment.NewLine + string.Join(Environment.NewLine, problems));

    private static Window Build(string name) => name switch
    {
        "Question" => new QuestionWindow("Start Auth, which you paused?", "Build wants to start Auth.", "_Allow and start", "_Refuse", "Decide later"),
        "Information" => QuestionWindow.Information("Can't make a verifier for Auth yet", "Its holdout folder is not there."),
        "TextPrompt" => new TextPromptWindow("Rename", "New title", "Auth"),
        "ProjectProperties" => ProjectProperties(),
        "Settings" => Settings(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not a dialog of this test"),
    };

    private static Window ProjectProperties()
    {
        TagEditorViewModel tags = new(() => ["cache", "auth"], () => { });
        tags.Load(["cache"]);
        tags.NewTag = "a";
        ProjectPropertiesViewModel properties = new("theseus", "Theseus bootstrap", "#5E4BB0", "project.md") { TagEditor = tags };

        return new ProjectPropertiesWindow(properties);
    }

    private static Window Settings()
    {
        string folder = Path.Combine(Path.GetTempPath(), "theseus-wb9-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        SettingsViewModel settings = new(new Theseus.Core.Settings.SettingsStore(Path.Combine(folder, "settings.json")), () => { })
        {
            Guard = new HoldoutGuardViewModel(new Theseus.Core.Links.HoldoutGuardInstallation(Path.Combine(folder, "claude"), folder)),
            FolderTrust = new FolderTrustViewModel(
                new Theseus.Core.Trust.ClaudeFolderTrust(Path.Combine(folder, ".claude.json"), Path.Combine(folder, "undo.json")), () => []),
            Tags = null,
        };
        settings.OpenCommand.Execute(null);
        settings.HoldoutEntries = [new("theseus", folder, Theseus.Core.Links.HoldoutSource.Recorded)];

        return new SettingsWindow { DataContext = settings };
    }

    private static ChainNodeViewModel Chain(string key, string title) =>
        new(key, title, "#C0392B", "waiting", IsReady: false, string.Empty, string.Empty, new RelayCommand(_ => { }));

    // The window is never shown, so Loaded never comes: the toolbar names its overflow button then, so it is raised here.
    private static void Relayout(MainWindow window)
    {
        ToolBar toolBar = (ToolBar)window.FindName("MainToolBar");
        toolBar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, toolBar));
        FrameworkElement content = (FrameworkElement)window.Content;
        content.Measure(new Size(1400, 760));
        content.Arrange(new Rect(0, 0, 1400, 760));
        content.UpdateLayout();
    }
}
