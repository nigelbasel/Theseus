using Theseus.Core.Abstractions;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>A hand-over records the piece it came from when it is made, and keeps it (1.1f D15, D6).</summary>
[TestFixture]
public class LinkBookParentPieceTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"theseus-links-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private LinkBook Open(Func<string, string?>? currentPieceOf) =>
        new(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock()) { CurrentPieceOf = currentPieceOf };

    [Test]
    public void AHandOver_RecordsTheFromCardsCurrentPiece_AtTheMomentItIsMade()
    {
        var current = "bym-mcp-gui-parity-spec";
        var book = Open(card => card == "spec" ? current : null);

        var link = book.Add(LinkKind.HandsOver, "spec", "w6").Link!;
        current = "a-later-piece";

        Assert.That(link.ParentPiece, Is.EqualTo("bym-mcp-gui-parity-spec"));
        Assert.That(book.Active.Single().ParentPiece, Is.EqualTo("bym-mcp-gui-parity-spec"), "never changed after");
    }

    [Test]
    public void AReportBack_RecordsNoParentPiece()
    {
        var book = Open(_ => "a-piece");
        book.Add(LinkKind.HandsOver, "spec", "w6");

        Assert.That(book.Add(LinkKind.ReportsBack, "w6", "spec").Link!.ParentPiece, Is.Null);
    }

    [Test]
    public void TheParentPiece_SurvivesSatisfyRemoveAndAReload()
    {
        var book = Open(_ => "the-spec");
        var link = book.Add(LinkKind.HandsOver, "spec", "w6", output: @"C:\work\DESIGN.md").Link!;

        book.Satisfy(link.Id, "read it");
        book.Remove(link.Id);

        Assert.That(Open(null).All.Single().ParentPiece, Is.EqualTo("the-spec"));
    }

    [Test]
    public void WithNoProjectNamingTheCard_ItHasNone()
    {
        Assert.That(Open(_ => null).Add(LinkKind.HandsOver, "a", "b").Link!.ParentPiece, Is.Null);
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
    }
}
