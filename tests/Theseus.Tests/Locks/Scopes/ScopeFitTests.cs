using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class ScopeFitTests
{
    [Test]
    public void ScopesOnDifferentResources_FitSideBySide()
    {
        ResourceTable table = ResourceTable.Seeded();

        Assert.That(table.Fits(Scope("compose:members-hub", "port:8080"), [Scope("compose:leadpro", "port:8081")]), Is.True);
    }

    [Test]
    public void TheSameExclusiveResource_DoesNotFitTwice()
    {
        ResourceTable table = ResourceTable.Seeded();

        Assert.That(table.Fits(Scope("sql-containers"), [Scope("sql-containers")]), Is.False);
    }

    [Test]
    public void SlotsFit_UntilTheirSumPassesTheCapacity()
    {
        ResourceTable table = ResourceTable.Seeded().With(new ResourceRule("cpu-build", 2, [], "measured"));
        IReadOnlyList<ResourceClaim> one = [new ResourceClaim("cpu-build", 1)];

        Assert.That(table.Fits(one, [one]), Is.True);
        Assert.That(table.Fits(one, [one, one]), Is.False);
    }

    [Test]
    public void AResourceTheTableDoesNotKnow_ClashesWithEveryHold()
    {
        ResourceTable table = ResourceTable.Seeded();

        Assert.That(table.Fits(Scope("gpu"), []), Is.True);
        Assert.That(table.Fits(Scope("gpu"), [Scope("llama")]), Is.False);
        Assert.That(table.Fits(Scope("llama"), [Scope("gpu")]), Is.False);
    }

    [Test]
    public void ARuledResource_IsNoLongerExclusiveOfEverything()
    {
        ResourceTable table = ResourceTable.Seeded().With(new ResourceRule("gpu", 1, ["llama"], "the model server uses it", "arbiter"));

        Assert.That(table.Fits(Scope("gpu"), [Scope("dev-stack")]), Is.True);
        Assert.That(table.Fits(Scope("gpu"), [Scope("llama")]), Is.False);
        Assert.That(table.Fits(Scope("llama"), [Scope("gpu")]), Is.False);
    }

    [Test]
    public void AKindRow_CoversEveryValueOfThatKind()
    {
        ResourceTable table = ResourceTable.Seeded();

        Assert.That(table.IsKnown("compose:anything"), Is.True);
        Assert.That(table.IsKnown("worktree:e:/work/x"), Is.True);
        Assert.That(table.IsKnown("sprocket:x"), Is.False);
    }

    [Test]
    public void TheTestsScope_ClashesWithEachOfItsResources()
    {
        ResourceTable table = ResourceTable.Seeded().With(new ResourceRule("cpu-build", 3, [], "measured"));
        IReadOnlyList<ResourceClaim> tests = PredefinedScopes.Find(LockNames.Tests, table)!;

        Assert.That(table.Fits(tests, [Scope("sql-containers")]), Is.False);
        Assert.That(table.Fits(tests, [[new ResourceClaim("cpu-build", 1)]]), Is.False);
        Assert.That(table.Fits(tests, [Scope("docker-build")]), Is.False);
        Assert.That(table.Fits(tests, [Scope("dev-stack")]), Is.True);
    }

    [Test]
    public void AnAmountAboveTheCapacity_AsksForEverySlotRatherThanNeverFitting()
    {
        ResourceTable table = ResourceTable.Seeded().With(new ResourceRule("cpu-build", 2, [], "measured"));

        Assert.That(table.Fits([new ResourceClaim("cpu-build", 5)], []), Is.True);
        Assert.That(table.Fits([new ResourceClaim("cpu-build", 1)], [[new ResourceClaim("cpu-build", 5)]]), Is.False);
    }

    [Test]
    public void Normalise_MergesRepeatsAndCleansWorktreePaths()
    {
        IReadOnlyList<ResourceClaim> scope = ResourceClaim.Normalise(
            [new ResourceClaim(@"Worktree:E:\Work\X\", 1), new ResourceClaim("worktree:e:/work/x", 1), new ResourceClaim("cpu-build", 1), new ResourceClaim("cpu-build", 2)]);

        Assert.That(scope, Is.EqualTo(new[] { new ResourceClaim("cpu-build", 2), new ResourceClaim("worktree:e:/work/x", 1) }));
    }

    private static IReadOnlyList<ResourceClaim> Scope(params string[] names) =>
        [.. names.Select(name => new ResourceClaim(name, 1))];
}
