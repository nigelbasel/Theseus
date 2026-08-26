using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class JiraKeyExtractorTests
{
    [Test]
    public void Extract_FindsTheKeysInTheRealGdprPrompt()
    {
        var keys = JiraKeyExtractor.Extract(
            "Pick up the GDPR link-not-data work for the MCP server (APPS-2170 / NDC-202).");

        Assert.That(keys, Is.EqualTo(new[] { "APPS-2170", "NDC-202" }));
    }

    [Test]
    public void Extract_FindsSeveralKeysBecauseAWorkstreamRoutinelySpansThem()
    {
        var keys = JiraKeyExtractor.Extract("Migrate the legacy jobs (APPS-1776 and APPS-1778, plus INT-376).");

        Assert.That(keys, Is.EqualTo(new[] { "APPS-1776", "APPS-1778", "INT-376" }));
    }

    [Test]
    public void Extract_IgnoresStatusDocumentFilenamesThatMerelyLookLikeKeys()
    {
        var keys = JiraKeyExtractor.Extract(
            "Read SESSION-STATUS-2026-08-21-identity-admin.md first (APPS-2122).");

        Assert.That(keys, Is.EqualTo(new[] { "APPS-2122" }));
    }

    [Test]
    public void Extract_IgnoresARestartDocumentBecauseItsPrefixIsTooLongForAProjectKey()
    {
        Assert.That(JiraKeyExtractor.Extract("the evidence is in SESSION-RESTART-2026-08-25.md"), Is.Empty);
    }

    [Test]
    public void Extract_ReportsEachKeyOnceHoweverOftenThePromptRepeatsIt()
    {
        var keys = JiraKeyExtractor.Extract("APPS-2170 blocks APPS-2170 which blocks APPS-2170.");

        Assert.That(keys, Is.EqualTo(new[] { "APPS-2170" }));
    }

    [Test]
    public void Extract_KeepsTheOrderTheKeysFirstAppearIn()
    {
        var keys = JiraKeyExtractor.Extract("NDC-202 came before APPS-2170.");

        Assert.That(keys, Is.EqualTo(new[] { "NDC-202", "APPS-2170" }));
    }

    [Test]
    public void Extract_IgnoresLowerCaseWordsThatHappenToBeHyphenated()
    {
        Assert.That(JiraKeyExtractor.Extract("apps-2170 and branch nigel/apps-2122-identity"), Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Extract_HandlesAnEmptyPrompt(string? prompt)
    {
        Assert.That(JiraKeyExtractor.Extract(prompt), Is.Empty);
    }
}
