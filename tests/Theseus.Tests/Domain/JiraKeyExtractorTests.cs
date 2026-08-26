using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class JiraKeyExtractorTests
{
    [Test]
    public void Extract_FindsTheKeysInTheRealGdprPrompt()
    {
        var keys = JiraKeyExtractor.Extract(
            "Pick up the GDPR link-not-data work for the MCP server (PROJ-101 / API-202).");

        Assert.That(keys, Is.EqualTo(new[] { "PROJ-101", "API-202" }));
    }

    [Test]
    public void Extract_FindsSeveralKeysBecauseAWorkstreamRoutinelySpansThem()
    {
        var keys = JiraKeyExtractor.Extract("Migrate the legacy jobs (PROJ-201 and PROJ-202, plus OPS-376).");

        Assert.That(keys, Is.EqualTo(new[] { "PROJ-201", "PROJ-202", "OPS-376" }));
    }

    [Test]
    public void Extract_IgnoresStatusDocumentFilenamesThatMerelyLookLikeKeys()
    {
        var keys = JiraKeyExtractor.Extract(
            "Read SESSION-STATUS-2026-08-21-admin-console.md first (PROJ-102).");

        Assert.That(keys, Is.EqualTo(new[] { "PROJ-102" }));
    }

    [Test]
    public void Extract_IgnoresARestartDocumentBecauseItsPrefixIsTooLongForAProjectKey()
    {
        Assert.That(JiraKeyExtractor.Extract("the evidence is in SESSION-RESTART-2026-08-25.md"), Is.Empty);
    }

    [Test]
    public void Extract_ReportsEachKeyOnceHoweverOftenThePromptRepeatsIt()
    {
        var keys = JiraKeyExtractor.Extract("PROJ-101 blocks PROJ-101 which blocks PROJ-101.");

        Assert.That(keys, Is.EqualTo(new[] { "PROJ-101" }));
    }

    [Test]
    public void Extract_KeepsTheOrderTheKeysFirstAppearIn()
    {
        var keys = JiraKeyExtractor.Extract("API-202 came before PROJ-101.");

        Assert.That(keys, Is.EqualTo(new[] { "API-202", "PROJ-101" }));
    }

    [Test]
    public void Extract_IgnoresLowerCaseWordsThatHappenToBeHyphenated()
    {
        Assert.That(JiraKeyExtractor.Extract("proj-101 and branch dev/proj-102-admin"), Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Extract_HandlesAnEmptyPrompt(string? prompt)
    {
        Assert.That(JiraKeyExtractor.Extract(prompt), Is.Empty);
    }
}
