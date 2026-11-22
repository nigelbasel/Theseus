using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

[TestFixture]
public class SearchHistoryTests
{
    [Test]
    public void TheLatestSearch_IsFirst_AndARepeatMovesUp()
    {
        var history = new SearchHistory();
        history.Add("one");
        history.Add("two");
        history.Add("one");

        Assert.That(history.Items, Is.EqualTo(new[] { "one", "two" }));
    }

    [Test]
    public void OnlyTheLastFew_AreKept()
    {
        var history = new SearchHistory();

        for (var index = 0; index < SearchHistory.Capacity + 3; index++)
        {
            history.Add($"search {index}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(history.Items, Has.Count.EqualTo(SearchHistory.Capacity));
            Assert.That(history.Items[0], Is.EqualTo($"search {SearchHistory.Capacity + 2}"));
        });
    }

    [Test]
    public void BlankText_IsNotKept()
    {
        var history = new SearchHistory();

        Assert.Multiple(() =>
        {
            Assert.That(history.Add("  "), Is.False);
            Assert.That(history.Items, Is.Empty);
        });
    }
}
