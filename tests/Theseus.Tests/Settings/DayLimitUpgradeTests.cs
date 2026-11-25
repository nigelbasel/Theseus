using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

/// <summary>
/// A settings file written before the daily limits went off holds $20 and $60 only because they were the defaults, so
/// they stop applying; a figure someone chose is kept, and so is one set after the upgrade (Nigel, 2026-11-25).
/// </summary>
[TestFixture]
public class DayLimitUpgradeTests
{
    private string _folder = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-daylimits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "settings.json");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    private TheseusSettings ReadAfterWriting(string json)
    {
        File.WriteAllText(_path, json);

        return new SettingsStore(_path).Read();
    }

    private const string OldDefaults = """
        {
          "budgets": { "runCostLimitUsd": 7.5, "runTokenLimit": 3000000, "runTimeLimitMinutes": 120,
                       "cardDayCostLimitUsd": 20.00, "projectDayCostLimitUsd": 60.00, "stopAtPercent": 90, "handoffAllowanceUsd": 1.00 },
          "projectBudgets": {
            "bug-fixing": { "runCostLimitUsd": 5, "cardDayCostLimitUsd": 20.00, "projectDayCostLimitUsd": 60.00 }
          }
        }
        """;

    [Test]
    public void TheOldDefaultDayLimits_StopApplying_InTheAppsAndEachProjectsBudget()
    {
        var read = ReadAfterWriting(OldDefaults);

        Assert.That((read.Budgets.CardDayCostLimitUsd, read.Budgets.ProjectDayCostLimitUsd), Is.EqualTo(((decimal?)null, (decimal?)null)));
        Assert.That((read.ProjectBudgets["bug-fixing"].CardDayCostLimitUsd, read.ProjectBudgets["bug-fixing"].ProjectDayCostLimitUsd),
            Is.EqualTo(((decimal?)null, (decimal?)null)));
    }

    [Test]
    public void TheUpgrade_KeepsThePerRunLimitsInTheSameBudget()
    {
        Assert.That(ReadAfterWriting(OldDefaults).Budgets.RunCostLimitUsd, Is.EqualTo(7.5m));
    }

    [Test]
    public void AnOldDayLimitSomeoneChose_IsKept()
    {
        var read = ReadAfterWriting("""{ "budgets": { "cardDayCostLimitUsd": 35, "projectDayCostLimitUsd": 90 } }""");

        Assert.That((read.Budgets.CardDayCostLimitUsd, read.Budgets.ProjectDayCostLimitUsd), Is.EqualTo(((decimal?)35m, (decimal?)90m)));
    }

    [Test]
    public void ADayLimitSetToTheOldFigureAfterTheUpgrade_IsKept_ThroughAWriteAndARead()
    {
        var store = new SettingsStore(_path);
        store.Write(ReadAfterWriting(OldDefaults) with
        {
            Budgets = TheseusSettings.Default.Budgets with { CardDayCostLimitUsd = 20m, ProjectDayCostLimitUsd = 60m },
        });

        var read = store.Read();

        Assert.That((read.Budgets.CardDayCostLimitUsd, read.Budgets.ProjectDayCostLimitUsd), Is.EqualTo(((decimal?)20m, (decimal?)60m)));
    }

    [Test]
    public void KeysInAnyCase_AreUpgraded_AndProjectsWhoseNamesDifferOnlyInCase_DoNotStopTheRead()
    {
        var read = ReadAfterWriting("""
            { "Budgets": { "CardDayCostLimitUsd": 20 },
              "projectBudgets": { "Foo": { "cardDayCostLimitUsd": 20 }, "foo": { "ProjectDayCostLimitUsd": 45 } } }
            """);

        Assert.That(read.Budgets.CardDayCostLimitUsd, Is.Null);
        Assert.That(read.ProjectBudgets.Values.Select(budget => budget.ProjectDayCostLimitUsd), Has.Some.EqualTo(45m));
    }

    [Test]
    public void AnUpgradedFile_ReadAgain_StillHasNoDayLimit()
    {
        var store = new SettingsStore(_path);
        store.Write(ReadAfterWriting(OldDefaults));

        Assert.That(store.Read().Budgets.CardDayCostLimitUsd, Is.Null);
    }
}
