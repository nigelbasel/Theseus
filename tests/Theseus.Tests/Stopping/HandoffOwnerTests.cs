using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>Whose a shared handover file is: the one card its name names (handoff-names AC 1).</summary>
[TestFixture]
public class HandoffOwnerTests
{
    private const string Shared = @"C:\notes\HANDOFF-configure-the-nurtur-ndc-mcp-server-in-the-claude-directory.md";

    [Test]
    public void TheCardTheFileIsNamedAfter_OwnsIt()
    {
        Assert.That(HandoffOwner.Of(Shared,
        [
            ("configure-the-nurtur-ndc-mcp-server-in-the-claude-directory", "Configure the Nurtur NDC mcp server in the claude directory"),
            ("ndc-mcp-server", "NDC MCP Server"),
        ]), Is.EqualTo("configure-the-nurtur-ndc-mcp-server-in-the-claude-directory"),
            "the longer name contains the shorter one; the closer match owns it");
    }

    [Test]
    public void ACardNamedByItsTitle_OwnsIt()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF-theseus.md", [("git-22", "Theseus"), ("other", "Other work")]),
            Is.EqualTo("git-22"));
    }

    [Test]
    public void AFileThatNamesNoSharer_HasNoOwner()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF.md", [("one", "One"), ("two", "Two")]), Is.Null,
            "nothing changes; the warning stays");
    }

    [Test]
    public void AFileThatNamesTwoSharersEqually_HasNoOwner()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF-alpha-beta.md", [("alpha", "Alpha"), ("beta", "Beta")]), Is.Null);
    }
}
