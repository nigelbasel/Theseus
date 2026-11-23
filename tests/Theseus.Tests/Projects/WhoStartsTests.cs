using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// In a running project a box says who starts it and why it is not running yet, and offers Start anyway… second
/// (project-view-usability item 5, Nigel 2026-11-23).
/// </summary>
[TestFixture]
public class WhoStartsTests
{
    [Test]
    public void Line_NamesTheLeadAndWhyItIsNotRunningYet()
    {
        Assert.That(WhoStarts.Line("BuildYourMarket MCP", "Spec", isLeadOrVerifier: false, waitsOnInput: false),
            Is.EqualTo("Spec starts it: BuildYourMarket MCP is running and decides what runs. Not running because its turn has not come yet."));
    }

    [Test]
    public void Line_SaysWhenItWaitsOnAHandOver()
    {
        Assert.That(WhoStarts.Line("P", "Lead", isLeadOrVerifier: false, waitsOnInput: true), Does.EndWith("it waits on a hand-over that is not ready yet."));
    }

    [Test]
    public void Line_IsNothing_WhereTheOwnerIsTheOneToStartIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WhoStarts.Line(null, "Lead", isLeadOrVerifier: false, waitsOnInput: false), Is.Null, "a stopped project or an ad hoc card");
            Assert.That(WhoStarts.Line("P", "Lead", isLeadOrVerifier: true, waitsOnInput: false), Is.Null, "the lead, or a verifier");
        });
    }
}
