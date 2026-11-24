using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App.Controls;

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
        ["Arrange", "KindBox"],
        ["Checklist", "row:Rows"],
        ["DayReport", "CloseButton"],
        ["Instructions", "Tabs"],
        ["NewProject", "NameBox"],
        ["NewWorkstream", "TitleBox"],
        ["PromptEdit", "editor:Editor"],
        ["Proposal", "BriefView"],
        ["Records", "row:Entries"],
        ["Spec", "PreviewView"],
    ];

    [TestCaseSource(nameof(FirstControls))]
    public void OnceLoaded_TheFocusIsOnTheFirstControl(string name, string first)
    {
        using OpenedWindow opened = OpenedWindow.Of(name);
        DependencyObject? focused = FocusManager.GetFocusedElement(opened.Window) as DependencyObject;

        Assert.That(focused, Is.Not.Null, "something has the focus when the window opens (B3)");
        Assert.That(focused, Is.SameAs(Expected(opened, first)), $"{name} starts at {first} (W43)");
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

    // Tab from the start, as the keyboard does, until it comes round again.
    private static List<DependencyObject> TabOrder(Window window)
    {
        window.Activate();
        Keyboard.ClearFocus();
        window.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        DialogHost.Pump();
        List<DependencyObject> order = [];

        while (Keyboard.FocusedElement is DependencyObject current && !order.Contains(current) && order.Count < 200)
        {
            order.Add(current);
            ((UIElement)current).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            DialogHost.Pump();
        }

        return order;
    }

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
        System.Windows.Media.VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);

    private static string Describe(List<DependencyObject> order) => string.Join(" > ", order.Select(Describe));

    private static string Describe(DependencyObject element) =>
        element is FrameworkElement { Name.Length: > 0 } named ? named.Name : element.GetType().Name;
}
