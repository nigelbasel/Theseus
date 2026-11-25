using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>
/// Confirming a verifier's report stops the verifier, as a Stop press would; nothing else ever stops a card without a
/// press (verifier-card AC 13; Nigel, 2026-11-19).
/// </summary>
[TestFixture]
public class VerifierEndingTests
{
    [Test]
    public void ConfirmingAVerifiersReport_StopsTheVerifier()
    {
        Assert.That(VerifierEnding.StopsOnConfirm(LinkKind.ReportsBack, WorkstreamRole.Verifier), Is.True);
    }

    [Test]
    public void ConfirmingAnyOtherCardsReport_StopsNothing()
    {
        Assert.That(VerifierEnding.StopsOnConfirm(LinkKind.ReportsBack, WorkstreamRole.None), Is.False);
    }

    [Test]
    public void ConfirmingAHandOverFromAVerifier_StopsNothing()
    {
        Assert.That(VerifierEnding.StopsOnConfirm(LinkKind.HandsOver, WorkstreamRole.Verifier), Is.False,
            "only its report is the end of its job");
    }
}
