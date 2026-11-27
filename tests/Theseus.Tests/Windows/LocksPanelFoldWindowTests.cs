using System.Windows;
using System.Windows.Controls;
using Theseus.App.Controls;
using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Locks;
using Theseus.Tests.Accessibility;
using Theseus.Tests.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>
/// The locks panel as the window builds it, over a real lock book (Nigel, 2026-11-26): each lock one named line that
/// unfolds, its queue a fold of its own, Expand all and Collapse all in the header, and nothing a screen reader cannot name.
/// </summary>
[TestFixture]
[NonParallelizable]
public class LocksPanelFoldWindowTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void EveryLock_IsOneNamedFoldLine_AndItsDetailsAreHiddenUntilUnfolded() =>
        WithPanel((pane, locks) =>
        {
            var lines = LockLines(pane);

            Assert.That(lines, Has.Count.EqualTo(locks.Rows.Count));
            Assert.That(lines.Select(Name), Does.Contain("llama, held by 1, 1 waiting"));
            Assert.That(Shown<TextBlock>(pane).Any(text => text.Text == "Release…"), Is.False, "folded, no hold's details show");
        });

    [Test]
    public void ExpandAll_ShowsEveryLocksDetails_AndCollapseAll_HidesThem() =>
        WithPanel((pane, locks) =>
        {
            var window = Window.GetWindow(pane)!;

            Click((Button)window.FindName("LocksExpandAll"));
            Relayout(pane);
            Assert.That(Shown<TextBlock>(pane).Select(text => text.Text), Does.Contain("Waiting, in order (1)"));

            Click((Button)window.FindName("LocksCollapseAll"));
            Relayout(pane);
            Assert.That(Shown<TextBlock>(pane).Select(text => text.Text), Does.Not.Contain("Waiting, in order (1)"));
        });

    [Test]
    public void TheQueue_FoldsOnItsOwn_LeavingTheLockOpen() =>
        WithPanel((pane, locks) =>
        {
            var llama = locks.Rows.Single(row => row.Name == LockNames.Llama);
            llama.Fold.IsExpanded = true;
            Relayout(pane);
            var queue = Shown<FoldToggle>(pane).Single(toggle => Name(toggle) == "Waiting, in order (1)");

            queue.IsChecked = false;
            Relayout(pane);

            Assert.That(llama.Fold.IsExpanded, Is.True);
            Assert.That(llama.QueueFold.IsExpanded, Is.False);
            Assert.That(Shown<TextBlock>(pane).Any(text => text.Text == "Cancel request"), Is.False);
        });

    [Test]
    public void TheHeaderButtons_AreNamed() =>
        WithPanel((pane, _) =>
        {
            var window = Window.GetWindow(pane)!;

            Assert.That(Name((Button)window.FindName("LocksExpandAll")), Is.EqualTo("Expand all"));
            Assert.That(Name((Button)window.FindName("LocksCollapseAll")), Is.EqualTo("Collapse all"));
        });

    [Test]
    public void ThePanel_FoldedOrUnfolded_AnnouncesNothingWrong() =>
        WithPanel((pane, locks) =>
        {
            Assert.That(AutomationTree.Problems(pane), Is.Empty);

            locks.ExpandAllCommand.Execute(null);
            Relayout(pane);

            Assert.That(AutomationTree.Problems(pane), Is.Empty);
        });

    private static void WithPanel(Action<FrameworkElement, LocksViewModel> test) =>
        MainWindowFixture.Run((window, _) =>
        {
            var directory = Path.Combine(Path.GetTempPath(), $"theseus-locks-pane-{Guid.NewGuid():N}");
            var book = new LockBook(
                new LockStateStore(Path.Combine(directory, "locks.json")),
                new LockHistory(Path.Combine(directory, "locks-history.jsonl")),
                new AlwaysAlive(),
                new FixedClock(Nine),
                table: ResourceTable.Seeded());
            book.Request(new LockRequest(LockNames.Llama, "first", "model", HoldMode.Session, SessionId: "s1"));
            book.Request(new LockRequest(LockNames.Llama, "second", "model", HoldMode.Session, SessionId: "s2"));

            using var locks = new LocksViewModel(book, () => Nine) { Settings = new MemorySettingsStore(), IsOpen = true };
            var pane = (FrameworkElement)window.FindName("LocksPane");
            pane.DataContext = new LocksPaneContext(locks);
            Relayout(pane);

            try
            {
                test(pane, locks);
            }
            finally
            {
                pane.DataContext = null;

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        });

    private static List<FoldToggle> LockLines(FrameworkElement pane)
    {
        var list = MainWindowFixtureTests.Descendants<ItemsControl>(pane).First(control => Name(control) == "Locks");

        return [.. MainWindowFixtureTests.Descendants<FoldToggle>(list).Where(toggle => toggle.Style == pane.FindResource("LockFold")
            && !Name(toggle).StartsWith("Waiting", StringComparison.Ordinal))];
    }

    private static IEnumerable<T> Shown<T>(FrameworkElement pane)
        where T : FrameworkElement =>
        MainWindowFixtureTests.Descendants<T>(pane).Where(IsLaidOutVisible);

    // The window is never shown, so IsVisible is false everywhere: an element shows when it and every parent is Visible.
    private static bool IsLaidOutVisible(FrameworkElement element)
    {
        DependencyObject? at = element;

        while (at is not null)
        {
            if (at is UIElement { Visibility: not Visibility.Visible })
            {
                return false;
            }

            at = System.Windows.Media.VisualTreeHelper.GetParent(at);
        }

        return true;
    }

    private static string Name(DependencyObject element) => System.Windows.Automation.AutomationProperties.GetName(element);

    private static void Click(Button button) => button.Command.Execute(button.CommandParameter);

    private static void Relayout(FrameworkElement pane)
    {
        var content = (FrameworkElement)Window.GetWindow(pane)!.Content;
        content.Measure(new Size(1400, 760));
        content.Arrange(new Rect(0, 0, 1400, 760));
        content.UpdateLayout();
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class AlwaysAlive : ILockLiveness
    {
        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => true;
    }
}
