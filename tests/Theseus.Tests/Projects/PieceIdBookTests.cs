using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>7.3: every piece has a stable id, kept across a re-slug and stamped on every card of the piece.</summary>
[TestFixture]
public class PieceIdBookTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private string _path = null!;
    private int _next;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"theseus-piece-ids-{Guid.NewGuid():N}.json");
        _next = 0;
    }

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void Update_GivesEachPieceAnIdOnce_AndKeepsItAcrossReadsAndRestarts()
    {
        Book().Update([Reading(("card-a", "slug-a"), ("card-b", "slug-b"))]);
        var restarted = Book();
        restarted.Update([Reading(("card-a", "slug-a"), ("card-b", "slug-b"))]);

        Assert.That((restarted.IdFor("p", "slug-a"), restarted.IdFor("p", "slug-b")), Is.EqualTo(("id-1", "id-2")));
        Assert.That(restarted.Pieces, Has.Count.EqualTo(2));
    }

    [Test]
    public void Update_KeepsTheIdWhenASlugIsRenamed()
    {
        var book = Book();
        book.Update([Reading(("card-a", "old-slug"))]);

        book.Update([Reading(("card-a", "new-slug"))]);

        Assert.That(book.IdFor("p", "new-slug"), Is.EqualTo("id-1"));
        Assert.That(book.IdFor("p", "old-slug"), Is.Null);
    }

    [Test]
    public void Update_GivesASecondPieceOfTheSameCardANewId_WhenTheFirstIsStillThere()
    {
        var book = Book();
        book.Update([Reading(("card-a", "first"))]);

        book.Update([Reading(("card-a", "first"), ("card-a", "second"))]);

        Assert.That(book.IdFor("p", "second"), Is.EqualTo("id-2"));
    }

    [Test]
    public void Update_DoesNotLetAVerifiersNewRowTakeThePieceItChecks_WhenThatPieceIsRenamed()
    {
        var book = Book();
        book.Update([Reading(("card-a", "slug-a"))]);
        book.IdOf(Card("verify-a") with { Role = WorkstreamRole.Verifier }, MemberOf,
            [new("lk-1", LinkKind.ReportsBack, "verify-a", "card-a", Nine, Link.MadeOnTheBoard)]);

        book.Update([Reading(("verify-a", "verify-slug-a"), ("card-a", "slug-a-renamed"))]);

        Assert.That((book.IdFor("p", "slug-a-renamed"), book.IdFor("p", "verify-slug-a")), Is.EqualTo(("id-1", "id-2")));
    }

    [Test]
    public void IdOf_GivesAVerifierThePieceOfTheCardItChecks()
    {
        var book = Book();
        book.Update([Reading(("card-a", "slug-a"))]);
        var verifier = Card("verify-a") with { Role = WorkstreamRole.Verifier };
        Link[] links = [new("lk-1", LinkKind.ReportsBack, "verify-a", "card-a", Nine, Link.MadeOnTheBoard)];

        Assert.That(book.IdOf(verifier, MemberOf, links), Is.EqualTo("id-1"));
    }

    [Test]
    public void IdOf_GivesAHandOverChildInNoRowItsParentsPiece_AndKeepsItAfterTheLinkGoes()
    {
        var book = Book();
        book.Update([Reading(("card-a", "slug-a"))]);
        Link[] links = [new("lk-1", LinkKind.HandsOver, "card-a", "child", Nine, Link.MadeOnTheBoard)];

        Assert.That(book.IdOf(Card("child"), MemberOf, links), Is.EqualTo("id-1"));
        Assert.That(Book().IdOf(Card("child"), MemberOf, []), Is.EqualTo("id-1"), "remembered on the piece");
    }

    [Test]
    public void IdOf_GivesAScenarioWriterNoPiece()
    {
        var book = Book();
        book.Update([Reading(("card-a", "slug-a"))]);

        Assert.That(book.IdOf(Card("card-a") with { Role = WorkstreamRole.ScenarioWriter }, MemberOf, []), Is.Null);
    }

    [Test]
    public void Members_CarryTheirPiecesId()
    {
        var book = Book();
        var readings = new[] { Reading(("card-a", "slug-a")) };
        book.Update(readings);

        var members = ProjectMembers.From(readings).WithPieceIds(member => book.IdFor(member.Project, member.Slug));

        Assert.That(members.Of("card-a")?.PieceId, Is.EqualTo("id-1"));
    }

    private static ProjectMember? MemberOf(string card) => card == "card-a" ? new ProjectMember("p", "slug-a") : null;

    private PieceIdBook Book() => new(_path, () => $"id-{++_next}");

    private static ProjectFileReading Reading(params (string Card, string Slug)[] rows) =>
        ProjectFileReader.Read("p", TestProjectsFolder.ProjectText("P", body:
            "## Phase 0: Start\n\n### Objective 0.1: Something\n- [ ] A criterion.\n\n**Pipeline:** feature\n"
            + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
            + string.Concat(rows.Select(row => $"| {row.Card} | {row.Slug} | owner/{row.Slug} | a-repo |\n"))));

    private static Workstream Card(string key) =>
        new(key, key, "#208DD7", ColorIsManual: true, @"C:\work", [], "Prompt.", 1, "New",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, null, [], null, DateTimeOffset.UtcNow);
}
