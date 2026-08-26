using Theseus.Core.Domain;
using Theseus.Core.Persistence;

namespace Theseus.Tests.Persistence;

[TestFixture]
public class PlanStoreTests
{
    private const string RealPlan = """
        {
            "order":  1,
            "title":  "GDPR MCP contact forms",
            "color":  "#C0392B",
            "cwd":  "C:\\dev\\tfsgit",
            "addDirs":  [
                            "C:\\dev\\tfsgit\\features\\apps-2170-gdpr-link-view\\Nurtur.Direct.Communications"
                        ],
            "resumeSessionId":  null,
            "writtenAt":  "2026-08-25T21:20:33",
            "prompt":  "Pick up the GDPR link-not-data work.\nREQ-06 stays in Shadow."
        }
        """;

    private string _directory = string.Empty;
    private PlanStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-plans-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _store = new PlanStore(_directory, attempts: 2, retryDelay: TimeSpan.Zero);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void TryRead_ReadsTheFormatTheExistingPlanFilesAreAlreadyWrittenIn()
    {
        Write("gdpr-mcp", RealPlan);

        var plan = _store.TryRead("gdpr-mcp");

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan!.Order, Is.EqualTo(1));
        Assert.That(plan.Title, Is.EqualTo("GDPR MCP contact forms"));
        Assert.That(plan.Color, Is.EqualTo("#C0392B"));
        Assert.That(plan.Cwd, Is.EqualTo(@"C:\dev\tfsgit"));
        Assert.That(plan.AddDirs, Has.Count.EqualTo(1));
        Assert.That(plan.ResumeSessionId, Is.Null);
        Assert.That(plan.WrittenAt, Is.EqualTo("2026-08-25T21:20:33"));
        Assert.That(plan.Prompt, Does.Contain("REQ-06"));
    }

    [Test]
    public void TryRead_ReadsAFileWrittenWithAByteOrderMarkBecausePowerShellWritesOne()
    {
        File.WriteAllText(Path.Combine(_directory, "gdpr-mcp.json"), RealPlan, new System.Text.UTF8Encoding(true));

        Assert.That(_store.TryRead("gdpr-mcp"), Is.Not.Null);
    }

    [Test]
    public void TryRead_ReportsAMissingPlanAsAbsentRatherThanThrowing()
    {
        Assert.That(_store.TryRead("never-written"), Is.Null);
    }

    [Test]
    public void TryRead_RejectsHalfFlushedJsonRatherThanReturningARuinedPlan()
    {
        Write("gdpr-mcp", RealPlan[..80]);

        Assert.That(_store.TryRead("gdpr-mcp"), Is.Null);
    }

    [TestCase("prompt")]
    [TestCase("cwd")]
    [TestCase("writtenAt")]
    public void TryRead_RejectsAPlanMissingAFieldALaunchCannotProceedWithout(string field)
    {
        Write("gdpr-mcp", RealPlan.Replace($"\"{field}\":", "\"unused\":"));

        Assert.That(_store.TryRead("gdpr-mcp"), Is.Null);
    }

    [Test]
    public void TryRead_RejectsAPlanWhosePromptIsPresentButEmpty()
    {
        Write("gdpr-mcp", """
            {"order":1,"title":"t","color":"#000000","cwd":"C:\\dev","addDirs":[],
             "resumeSessionId":null,"writtenAt":"2026-08-25T21:20:33","prompt":"   "}
            """);

        Assert.That(_store.TryRead("gdpr-mcp"), Is.Null);
    }

    [Test]
    public void Keys_ListsEveryPlanInAStableOrder()
    {
        Write("legacy-jobs", RealPlan);
        Write("gdpr-mcp", RealPlan);
        Write("identity-admin", RealPlan);

        Assert.That(_store.Keys(), Is.EqualTo(new[] { "gdpr-mcp", "identity-admin", "legacy-jobs" }));
    }

    [Test]
    public void Keys_IsEmptyWhenTheDirectoryDoesNotExistYet()
    {
        var store = new PlanStore(Path.Combine(_directory, "not-created"));

        Assert.That(store.Keys(), Is.Empty);
    }

    [Test]
    public void Write_RoundTripsAPlanThroughDisk()
    {
        var plan = new SessionPlan(2, "Legacy job migration", "#D68910", @"C:\dev\tfsgit",
            [@"C:\dev\tfsgit\JobDispatcher"], "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa",
            "2026-08-26T09:00:00", "Carry on.");

        _store.Write("legacy-jobs", plan);

        var read = _store.TryRead("legacy-jobs");
        Assert.That(read, Is.Not.Null);
        Assert.That(read! with { AddDirs = [] }, Is.EqualTo(plan with { AddDirs = [] }));
        Assert.That(read.AddDirs, Is.EqualTo(plan.AddDirs));
    }

    [Test]
    public void Write_LeavesNoTemporaryFileBehindForTheWatcherToTripOver()
    {
        _store.Write("legacy-jobs", new SessionPlan(1, "t", "#000000", @"C:\dev", [], null,
            "2026-08-26T09:00:00", "Carry on."));

        Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Write_ReplacesAnExistingPlanRatherThanAppendingToIt()
    {
        Write("gdpr-mcp", RealPlan);
        _store.Write("gdpr-mcp", new SessionPlan(9, "Renamed", "#000000", @"C:\dev", [], null,
            "2026-08-26T09:00:00", "New prompt."));

        Assert.That(_store.TryRead("gdpr-mcp")!.Title, Is.EqualTo("Renamed"));
    }

    [Test]
    public void PathFor_NamesTheFileAfterTheKeySoAKeyAndItsPlanCanNeverDisagree()
    {
        Assert.That(_store.PathFor("gdpr-mcp"), Is.EqualTo(Path.Combine(_directory, "gdpr-mcp.json")));
    }

    private void Write(string key, string contents) =>
        File.WriteAllText(Path.Combine(_directory, $"{key}.json"), contents);
}
