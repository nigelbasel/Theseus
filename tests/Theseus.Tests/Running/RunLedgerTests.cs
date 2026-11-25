using Theseus.Core.Budgets;
using Theseus.Core.Running;
using static Theseus.Tests.Running.RunRecords;

namespace Theseus.Tests.Running;

[TestFixture]
public class RunLedgerTests
{
    private string _path = null!;

    [SetUp]
    public void Fresh() => _path = Path.Combine(Path.GetTempPath(), "theseus-ledger-" + Guid.NewGuid().ToString("N"), "run-ledger.jsonl");

    [TearDown]
    public void Remove()
    {
        if (Directory.Exists(Path.GetDirectoryName(_path)))
        {
            Directory.Delete(Path.GetDirectoryName(_path)!, recursive: true);
        }
    }

    [Test]
    public void ARecord_ReadsBackAsWritten()
    {
        var ledger = new RunLedger(_path);
        var record = Run(cost: 1.25m) with { Limit = BudgetLimit.RunTokens, LimitDetail = "x", Tokens = new RunTokens(1, 2, 3, 4) };

        ledger.Append(record);

        Assert.That(ledger.Read().Single(), Is.EqualTo(record));
    }

    [Test]
    public void ADamagedLine_CostsOnlyThatRun()
    {
        var ledger = new RunLedger(_path);
        ledger.Append(Run());
        File.AppendAllText(_path, "{not json\n");
        ledger.Append(Run());

        Assert.That(ledger.Read(), Has.Count.EqualTo(2));
    }
}
