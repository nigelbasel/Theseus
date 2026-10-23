using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class SessionPathRuleTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly SessionPathRule _rule = new(@"C:\plans", @"C:\data\Theseus");

    [Test]
    public void APathInTheBriefFolderIsAllowed()
    {
        Assert.That(_rule.Refusal(@"C:\work\proposals\token-spike\REPORT.md", Board(Card("token-spike"))), Is.Null);
    }

    [Test]
    public void AStoredHandoffIsRefused()
    {
        var board = Board(Card("token-spike", stored: @"C:\work\HANDOFF-old.md"));

        Assert.That(_rule.Refusal(@"c:\work\handoff-old.md", board), Does.Contain("handoff"));
    }

    [Test]
    public void AResolvedHandoffIsRefused()
    {
        var board = Board(Card("token-spike", handoff: @"C:\work\HANDOFF-token-spike.md"));

        Assert.That(_rule.Refusal(@"C:\work\HANDOFF-token-spike.md", board), Does.Contain("handoff"));
    }

    [Test]
    public void ASessionStatusDocumentAtAWorkingDirectoryRootIsRefused()
    {
        Assert.That(_rule.Refusal(@"C:\work\SESSION-STATUS-2026-10-01-x.md", Board(Card("token-spike"))),
            Does.Contain("session status"));
    }

    [TestCase(@"C:\plans\token-spike.json")]
    [TestCase(@"C:\data\Theseus\links.json")]
    public void ThePlanAndDataDirectoriesAreRefused(string path)
    {
        Assert.That(_rule.Refusal(path, Board(Card("token-spike"))), Is.Not.Null);
    }

    private static BoardSnapshot Board(params BoardSnapshotCard[] cards) => new(cards, Nine);

    private static BoardSnapshotCard Card(string key, string? stored = null, string? handoff = null) =>
        new(new Workstream(key, key, "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, false,
                false, null, [], stored, Nine),
            handoff ?? stored ?? $@"C:\work\HANDOFF-{key}.md", SessionLifecycle.NotStarted, false, false, null, false);
}
