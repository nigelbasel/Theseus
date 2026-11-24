using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The status bar's notice carries its severity (baseline WB7; finding C17; W46): a warning or an error says so, and a
/// plain notice after it is information again.
/// </summary>
[TestFixture]
public class NoticeLineTests
{
    [Test]
    public void APlainNotice_IsInformation()
    {
        NoticeLine line = new();

        line.Say("Started alpha.");

        Assert.That((line.Text, line.Severity), Is.EqualTo(("Started alpha.", NoticeSeverity.Information)));
    }

    [Test]
    public void MoreSaidAfterAWarning_StaysAWarning_AndAfterNothing_IsInformation()
    {
        NoticeLine line = new();

        Assert.That(line.SeverityToExtend, Is.EqualTo(NoticeSeverity.Information));

        line.Say(NoticeSeverity.Warning, "alpha did not start.");

        Assert.That(line.SeverityToExtend, Is.EqualTo(NoticeSeverity.Warning));
    }

    [Test]
    public void APlainNotice_AfterAnError_IsInformationAgain()
    {
        NoticeLine line = new();
        line.Say(NoticeSeverity.Error, "alpha.md could not be opened: access denied");

        Assert.That(line.Severity, Is.EqualTo(NoticeSeverity.Error));

        line.Say("Started alpha.");

        Assert.That(line.Severity, Is.EqualTo(NoticeSeverity.Information));
    }
}
