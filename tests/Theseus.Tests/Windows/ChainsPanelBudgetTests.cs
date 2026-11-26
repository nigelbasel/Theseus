using System.Diagnostics;
using System.Windows;
using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.Windows;

/// <summary>
/// The tree is virtualised (chains-panel-at-scale criterion 19, as Nigel decided on 2026-11-25 when 300 rows measured over
/// two seconds laid out whole): however many rows there are, only those in view are built. Proved by counting what is
/// built, not by a time alone, since a shared machine's timings vary threefold from run to run; the time limit is a
/// generous guard. A search keystroke over 300 rows refilters in under 50 ms. Times are the best of three.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ChainsPanelBudgetTests
{
    private const int Rows = 300;

    [Test]
    public void AThousandRows_BuildOnlyThoseInView() =>
        MainWindowFixture.Run((window, shell) =>
        {
            var pane = (FrameworkElement)window.FindName("ChainsPane");
            pane.Visibility = Visibility.Visible;
            var content = (FrameworkElement)window.Content;
            var best = double.MaxValue;

            for (var pass = 0; pass < 3; pass++)
            {
                shell.Chains.Roots.Clear();
                content.UpdateLayout();
                var clock = Stopwatch.StartNew();

                foreach (var chain in Chains(1000))
                {
                    shell.Chains.Roots.Add(chain);
                }

                content.Measure(new Size(1400, 760));
                content.Arrange(new Rect(0, 0, 1400, 760));
                content.UpdateLayout();
                best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
            }

            var built = MainWindowFixtureTests.Descendants<System.Windows.Controls.TreeViewItem>(pane).Count();
            TestContext.Out.WriteLine($"best layout of 1000 rows: {best:0} ms; rows built {built}");

            Assert.That(built, Is.LessThan(60), "a 760-pixel window shows a few dozen rows at most; the rest are not built");
            Assert.That(best, Is.LessThan(2000), "measured at 200 to 650 ms on a loaded workstation");
        });

    [Test]
    public void ASearchKeystrokeOverThreeHundredRows_RefiltersInUnder50Milliseconds()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"theseus-chains-budget-{Guid.NewGuid():N}");

        try
        {
            var book = new LinkBook(new LinkStateStore(Path.Combine(directory, "links.json")), new FixedClock());
            List<WorkstreamCardViewModel> cards = [];

            for (var index = 0; index < Rows; index++)
            {
                cards.Add(new WorkstreamCardViewModel(new Workstream($"card-{index}", $"Card {index} of the long chain", "#208DD7", false,
                    @"C:\work", [], "Carry on.", 0, "New", false, false, false, null, [], null, DateTimeOffset.UnixEpoch)));

                if (index % 10 != 0)
                {
                    book.Add(LinkKind.HandsOver, $"card-{index - 1}", $"card-{index}");
                }
                else if (index > 0)
                {
                    book.Add(LinkKind.ReportsBack, $"card-{index}", $"card-{index - 10}", output: @"C:\work\REPORT.md");
                }
            }

            var chains = new ChainsViewModel(book, () => cards, () => BriefFolder.DefaultName, () => { }, _ => { });
            chains.Update();
            var best = double.MaxValue;

            for (var pass = 0; pass < 3; pass++)
            {
                chains.SearchText = string.Empty;
                var clock = Stopwatch.StartNew();
                chains.SearchText = $"card 2{pass}";
                best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
            }

            TestContext.Out.WriteLine($"best refilter of {Rows} rows: {best:0.0} ms");
            Assert.That(best, Is.LessThan(50));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // Chains of ten: a lead and the nine cards it handed over to, as a project's lead holds its workstreams.
    private static IEnumerable<ChainNodeViewModel> Chains(int rows)
    {
        for (var chain = 0; chain < rows / 10; chain++)
        {
            var root = Node($"Chain {chain} lead");

            for (var step = 1; step < 10; step++)
            {
                root.Children.Add(Node($"Chain {chain} step {step}"));
            }

            yield return root;
        }
    }

    private static ChainNodeViewModel Node(string title) =>
        new(title.Replace(' ', '-'), title, "#C0392B", string.Empty, false, string.Empty, string.Empty, new RelayCommand(_ => { }))
        {
            RowState = ChainRowState.Running,
        };

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
    }
}
