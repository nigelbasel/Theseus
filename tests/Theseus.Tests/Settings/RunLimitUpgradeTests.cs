using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

/// <summary>
/// A settings file written before the per-run limits went off holds $5, 3,000,000 tokens and 120 minutes only because they
/// were the defaults, written back with the rest of the budget, so they stop applying; a figure someone chose is kept, and
/// so is one set after the upgrade (Nigel, 2026-11-25).
/// </summary>
[TestFixture]
public class RunLimitUpgradeTests
{
    private string _folder = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-runlimits-" + Guid.NewGuid().ToString("N"));
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
          "budgets": { "runCostLimitUsd": 5.00, "runTokenLimit": 3000000, "runTimeLimitMinutes": 120, "stopAtPercent": 90 },
          "breakers": { "crashes": 3, "noProgress": 3, "sameError": 3, "overTimeGraceMinutes": 15 },
          "projectBudgets": { "bug-fixing": { "runCostLimitUsd": 5, "runTokenLimit": 3000000, "runTimeLimitMinutes": 120 } }
        }
        """;

    [Test]
    public void TheOldDefaultRunLimits_StopApplying_InTheAppsAndEachProjectsBudget()
    {
        var read = ReadAfterWriting(OldDefaults);

        foreach (var budget in new[] { read.Budgets, read.ProjectBudgets["bug-fixing"] })
        {
            Assert.That((budget.RunCostLimitUsd, budget.RunTokenLimit, budget.RunTimeLimitMinutes),
                Is.EqualTo(((decimal?)null, (long?)null, (int?)null)));
        }
    }

    [Test]
    public void TheNoProgressBreaker_StaysOn()
    {
        Assert.That(ReadAfterWriting(OldDefaults).Breakers.NoProgress, Is.EqualTo(3));
    }

    [Test]
    public void AnOldRunLimitSomeoneChose_IsKept()
    {
        var read = ReadAfterWriting("""{ "budgets": { "runCostLimitUsd": 7.5, "runTokenLimit": 4000000, "runTimeLimitMinutes": 240 } }""");

        Assert.That((read.Budgets.RunCostLimitUsd, read.Budgets.RunTokenLimit, read.Budgets.RunTimeLimitMinutes),
            Is.EqualTo(((decimal?)7.5m, (long?)4_000_000L, (int?)240)));
    }

    [Test]
    public void ARunLimitSetToTheOldFigureAfterTheUpgrade_IsKept_ThroughAWriteAndARead()
    {
        var store = new SettingsStore(_path);
        store.Write(ReadAfterWriting(OldDefaults) with
        {
            Budgets = TheseusSettings.Default.Budgets with { RunCostLimitUsd = 5m, RunTokenLimit = 3_000_000, RunTimeLimitMinutes = 120 },
        });

        var read = store.Read();

        Assert.That((read.Budgets.RunCostLimitUsd, read.Budgets.RunTokenLimit, read.Budgets.RunTimeLimitMinutes),
            Is.EqualTo(((decimal?)5m, (long?)3_000_000L, (int?)120)));
    }

    [Test]
    public void AnUpgradedFile_ReadAgain_StillHasNoRunLimit()
    {
        var store = new SettingsStore(_path);
        store.Write(ReadAfterWriting(OldDefaults));

        var read = store.Read();

        Assert.That((read.Budgets.RunCostLimitUsd, read.Budgets.RunTimeLimitMinutes), Is.EqualTo(((decimal?)null, (int?)null)));
    }
}
