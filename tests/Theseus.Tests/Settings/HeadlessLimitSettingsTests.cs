using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

[TestFixture]
public class HeadlessLimitSettingsTests
{
    private static HeadlessLimitSetting Named(string label) => HeadlessLimitSettings.All.Single(setting => setting.Label == label);

    [Test]
    public void EveryLimit_StatesItsDefault()
    {
        Assert.That(HeadlessLimitSettings.All, Has.All.Matches<HeadlessLimitSetting>(setting => setting.Explanation.Contains("Default", StringComparison.Ordinal)));
    }

    [Test]
    public void ACost_IsShownAndTakenInDollars()
    {
        var setting = Named("Cost per run");
        var changed = setting.Apply(TheseusSettings.Default, "$7.50")!;

        Assert.That(changed.Budgets.RunCostLimitUsd, Is.EqualTo(7.5m));
        Assert.That(setting.Show(changed), Is.EqualTo("$7.50"));
    }

    [Test]
    public void ACount_TakesThousandsSeparators()
    {
        Assert.That(Named("Tokens per run").Apply(TheseusSettings.Default, "4,000,000")!.Budgets.RunTokenLimit, Is.EqualTo(4_000_000));
    }

    [TestCase("")]
    [TestCase("0")]
    [TestCase("-3")]
    [TestCase("lots")]
    public void AValueThatIsNotAPositiveNumber_IsRefused(string text)
    {
        Assert.That(Named("Headless runs at once").Apply(TheseusSettings.Default, text), Is.Null);
    }
}
