using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App;
using Theseus.App.Controls;
using Theseus.App.ViewModels;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.Windows;

/// <summary>
/// Where the keyboard starts in each secondary window, and the order Tab takes through it (batch WB5; baseline B2, B3,
/// B-m4, B-m5; decisions W42, W43; checklist G65, G66, G67): the first field of a form, the editor of an editor, the first
/// row of a list, Close in a window that only shows something; and every control of the content before the footer.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SecondaryWindowFocusTests
{
    // "row:X" is the first row of the list X; "editor:X" the editor inside the Markdown box X; otherwise an element's name.
    private static readonly object[][] FirstControls =
    [
        ["About", "CloseButton"],
        ["AddObjective", "TitleBox"],
        ["AddToProject", "ProjectBox"],
        ["Arrange", "row:Groups"],
        ["Checklist", "row:Rows"],
        ["DayReport", "CloseButton"],
        ["Instructions", "FilesTab"],
        ["NewProject", "TitleBox"],
        ["NewWorkstream", "TitleBox"],
        ["PromptEdit", "editor:Editor"],
        ["Proposal", "BriefView"],
        ["Records", "row:Entries"],
        ["Spec", "PreviewView"],
        ["Waits", "row:Rows"],
    ];

    [TestCaseSource(nameof(FirstControls))]
    public void OnceLoaded_TheFocusIsOnTheFirstControl(string name, string first)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        DependencyObject? focused = FocusManager.GetFocusedElement(opened.Window) as DependencyObject;

        Assert.That(focused, Is.Not.Null, "something has the focus when the window opens (B3)");
        Assert.That(focused, Is.SameAs(Expected(opened, first)), $"{name} starts at {first} (W43)");
    }

    [Test]
    public void TheRecordsWindow_StartsOnTheChosenRow_NotTheFirst()
    {
        using TestProjectsFolder folder = TestProjectsFolder.Create();
        RecordsViewModel model = new(Named(BymPieces(folder), "bym-mcp-identity"), "Plan", _ => { });
        Window window = DialogHost.Open(new RecordsWindow(model));

        try
        {
            ListBox entries = (ListBox)window.FindName("Entries");

            Assert.That(entries.SelectedIndex, Is.GreaterThan(0), "the stage asked for is not the first row");
            Assert.That(FocusManager.GetFocusedElement(window), Is.SameAs(entries.ItemContainerGenerator.ContainerFromIndex(entries.SelectedIndex)),
                "the keyboard starts where the shown document is, so an arrow moves from there (W43)");
        }
        finally
        {
            window.Close();
        }
    }

    [TestCaseSource(typeof(SecondaryWindowNames), nameof(SecondaryWindowNames.Every))]
    public void Tab_VisitsTheWholeContent_BeforeTheFooter(string name)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        Panel footer = opened.Named<Panel>("Footer");
        List<DependencyObject> order = TabOrder(opened.Window);
        int firstInFooter = order.FindIndex(element => IsIn(element, footer));

        Assert.That(firstInFooter, Is.GreaterThan(0), $"Tab reaches the footer, after the content: {Describe(order)}");
        Assert.That(order.Skip(firstInFooter).Where(element => !IsIn(element, footer)).Select(Describe), Is.Empty,
            $"nothing of the content comes after the footer (B2): {Describe(order)}");
    }

    [TestCase("Spec", "ViewChoice")]
    [TestCase("Records", "ViewChoice")]
    [TestCase("Checklist", "Rows")]
    [TestCase("Waits", "Rows")]
    [TestCase("Arrange", "Groups")]
    public void AGroup_IsOneTabStop_AndTheArrowsStayInsideIt(string name, string group)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        FrameworkElement container = opened.Named<FrameworkElement>(group);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.GetTabNavigation(container), Is.EqualTo(KeyboardNavigationMode.Once), "one Tab stop (W42)");
            Assert.That(KeyboardNavigation.GetDirectionalNavigation(container), Is.EqualTo(KeyboardNavigationMode.Contained), "no wrap (W42)");
        });
    }

    private static DependencyObject Expected(OpenedWindow opened, string first)
    {
        if (first.StartsWith("row:", StringComparison.Ordinal))
        {
            ItemsControl list = opened.Named<ItemsControl>(first[4..]);
            DependencyObject container = list.ItemContainerGenerator.ContainerFromIndex(0);

            return container is ContentPresenter presenter
                ? DialogHost.Descendants(presenter).OfType<Control>().First(control => control.Focusable)
                : container;
        }

        if (first.StartsWith("editor:", StringComparison.Ordinal))
        {
            return opened.Named<MarkdownBox>(first[7..]).Editor;
        }

        return opened.Named<DependencyObject>(first);
    }

    // The order Tab takes, worked out as WPF does: the drawn tab stops by TabIndex, then in tree order. Moving the
    // keyboard for real needs an interactive desktop, which a CI runner does not have.
    // A link inside text, such as About's, is a content element: it takes its turn where its text block stands.
    private static List<DependencyObject> TabOrder(Window window) =>
        [.. DialogHost.Descendants(window)
            .SelectMany(element => element is TextBlock text ? [text, .. text.Inlines.OfType<System.Windows.Documents.Hyperlink>()] : new[] { element })
            .Where(IsTabStop)
            .Select((element, index) => (Element: element, Index: index))
            .OrderBy(each => KeyboardNavigation.GetTabIndex(each.Element)).ThenBy(each => each.Index)
            .Select(each => each.Element)];

    private static bool IsTabStop(DependencyObject element) => element switch
    {
        UIElement { IsVisible: true, IsEnabled: true, Focusable: true } => KeyboardNavigation.GetIsTabStop(element),
        ContentElement { IsEnabled: true, Focusable: true } => KeyboardNavigation.GetIsTabStop(element),
        _ => false,
    };

    private static bool IsIn(DependencyObject element, DependencyObject ancestor)
    {
        for (DependencyObject? at = element; at is not null; at = Parent(at))
        {
            if (ReferenceEquals(at, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? Parent(DependencyObject element) =>
        (element is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);

    private static string Describe(List<DependencyObject> order) => string.Join(" > ", order.Select(Describe));

    private static string Describe(DependencyObject element) =>
        element is FrameworkElement { Name.Length: > 0 } named ? named.Name : element.GetType().Name;
}
