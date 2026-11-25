using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>A waiting note's asks: one note may list several, each on its own whom (project-view-scale, 2026-11-24).</summary>
[TestFixture]
public class CriterionWaitingTests
{
    [Test]
    public void ANoteOfOneAsk_HasThatAsk()
    {
        var waiting = CriterionWaiting.Parse("Waiting: owner: Owner names the reviewer")!;

        Assert.That(waiting.Asks, Is.EqualTo(new[] { new WaitingAsk(true, "Owner names the reviewer") }));
    }

    [Test]
    public void ANoteOfSeveralAsks_SplitsAtEachWhom_InOrder()
    {
        var waiting = CriterionWaiting.Parse("Waiting: owner: A names B; work: the store (1.3); owner: the team: an answer; work: C")!;

        Assert.That(waiting.Asks, Is.EqualTo(new[]
        {
            new WaitingAsk(true, "A names B"), new WaitingAsk(false, "the store (1.3)"), new WaitingAsk(true, "the team: an answer"),
            new WaitingAsk(false, "C"),
        }));
        Assert.That(waiting.Line, Is.EqualTo("Waiting: owner: A names B; work: the store (1.3); owner: the team: an answer; work: C"),
            "the file's line is kept as written");
    }

    [Test]
    public void AWorkNoteWithAnOwnerAskLater_StillWaitsOnTheOwner()
    {
        var criterion = new ProjectCriterion("A criterion.", false, 1) { Waiting = CriterionWaiting.Parse("Waiting: work: built; owner: Owner decides") };

        Assert.That((criterion.WaitsOnOwner, criterion.Waiting!.OwnerAsks.Single().What), Is.EqualTo((true, "Owner decides")));
    }

    [Test]
    public void ASemicolonInsideAnAsk_DoesNotSplitIt()
    {
        var waiting = CriterionWaiting.Parse("Waiting: owner: figures; and the slice definition")!;

        Assert.That(waiting.Asks.Single().What, Is.EqualTo("figures; and the slice definition"));
    }

    [Test]
    public void AnAskOnSomeoneByName_IsReadAndWrittenInTheOnForm()
    {
        var waiting = CriterionWaiting.Parse("Waiting: on the gateway team: the EC answers; owner: decide")!;

        Assert.That(waiting.Asks, Is.EqualTo(new[] { new WaitingAsk(false, "the EC answers") { On = "the gateway team" }, new WaitingAsk(true, "decide") }));
        Assert.That(waiting.Line, Is.EqualTo("Waiting: on the gateway team: the EC answers; owner: decide"));
        Assert.That(CriterionWaiting.Join(waiting.Asks), Is.EqualTo("on the gateway team: the EC answers; owner: decide"));
        Assert.That(waiting.Asks[0].Who([]), Is.EqualTo("the gateway team"), "named, so no owner's name is needed");
    }

    [Test]
    public void AnOlderOwnerLine_NamesSomeoneElse_OnlyWhenItsWhoDoesNotNameTheOwner()
    {
        IReadOnlyList<string> names = ["owner-account", "Owner Person", "Owner"];

        Assert.That(new WaitingAsk(true, "Gateway owners: EC answers").Who(names), Is.EqualTo("Gateway owners"));
        Assert.That(new WaitingAsk(true, "The team with Owner: a budget").Who(names), Is.Null);
        Assert.That(new WaitingAsk(true, "owner-account names the reviewer").Who(names), Is.Null);
        Assert.That(new WaitingAsk(true, "Owner's rule: minimums set later").Who(names), Is.Null);
        Assert.That(new WaitingAsk(false, "Gateway owners: EC answers").Who(names), Is.Null, "a work ask is on no one");
        Assert.That(new WaitingAsk(true, "Decide: A or B").Who(names), Is.EqualTo("Decide"), "one word reads as a whom; the on form is the sure way");
        Assert.That(new WaitingAsk(true, "confirm the URL with the team: https://example.org").Who(names), Is.Null,
            "more than four words is a sentence, not a whom");
    }

    [Test]
    public void AnOnFormNamingTheOwner_IsTheOwners()
    {
        IReadOnlyList<string> names = ["owner-account", "Owner Person", "Owner"];

        Assert.That(new WaitingAsk(false, "decide") { On = "Owner" }.Who(names), Is.Null);
        Assert.That(new WaitingAsk(false, "decide") { On = "me" }.Who([]), Is.Null);
    }

    [TestCase("the gateway team", true)]
    [TestCase("Legal: Ana", false)]
    [TestCase("legal; ops", false)]
    [TestCase("", false)]
    public void AWhom_IsWritable_OnlyAsTheGrammarReadsItBack(string who, bool writable)
    {
        Assert.That(WaitingAsk.IsWritableWho(who), Is.EqualTo(writable));
    }
}
