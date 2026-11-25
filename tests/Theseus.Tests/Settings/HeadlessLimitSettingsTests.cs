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

    [TestCase("Cost per card per day")]
    [TestCase("Cost per project per day")]
    public void ADayLimit_IsOffByDefault_AndSaysSo(string label)
    {
        var setting = Named(label);

        Assert.That(setting.Show(TheseusSettings.Default), Is.EqualTo("Off"));
        Assert.That(setting.Explanation, Does.Contain("Default off"));
    }

    [Test]
    public void ADayLimit_CanBeSwitchedOn_AndOffAgain()
    {
        var setting = Named("Cost per card per day");
        var on = setting.Apply(TheseusSettings.Default, "$25")!;

        Assert.That((on.Budgets.CardDayCostLimitUsd, setting.Show(on)), Is.EqualTo(((decimal?)25m, "$25.00")));
        Assert.That(setting.Apply(on, "off")!.Budgets.CardDayCostLimitUsd, Is.Null);
        Assert.That(setting.Apply(on, "")!.Budgets.CardDayCostLimitUsd, Is.Null, "an empty value is no limit");
    }

    [Test]
    public void ADayLimitThatIsNotAPositiveNumber_IsRefused()
    {
        Assert.That(Named("Cost per project per day").Apply(TheseusSettings.Default, "-3"), Is.Null);
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
