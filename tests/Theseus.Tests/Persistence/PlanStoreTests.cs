using Theseus.Core.Domain;
using Theseus.Core.Persistence;

namespace Theseus.Tests.Persistence;

[TestFixture]
public class PlanStoreTests
{
    private const string RealPlan = """
        {
            "order":  1,
            "title":  "Contact form validation",
            "color":  "#C0392B",
            "cwd":  "C:\\work\\repo",
            "addDirs":  [
                            "C:\\work\\repo\\features\\contact-forms"
                        ],
            "resumeSessionId":  null,
            "writtenAt":  "2026-08-25T21:20:33",
            "prompt":  "Pick up the contact form work.\nREQ-06 stays in review."
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
        Write("contact-forms", RealPlan);

        var plan = _store.TryRead("contact-forms");

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan!.Order, Is.EqualTo(1));
        Assert.That(plan.Title, Is.EqualTo("Contact form validation"));
        Assert.That(plan.Color, Is.EqualTo("#C0392B"));
        Assert.That(plan.Cwd, Is.EqualTo(@"C:\work\repo"));
        Assert.That(plan.AddDirs, Has.Count.EqualTo(1));
        Assert.That(plan.ResumeSessionId, Is.Null);
        Assert.That(plan.WrittenAt, Is.EqualTo("2026-08-25T21:20:33"));
        Assert.That(plan.Prompt, Does.Contain("REQ-06"));
    }

    [Test]
    public void TryRead_ReadsAFileWrittenWithAByteOrderMarkBecausePowerShellWritesOne()
    {
        File.WriteAllText(Path.Combine(_directory, "contact-forms.json"), RealPlan, new System.Text.UTF8Encoding(true));

        Assert.That(_store.TryRead("contact-forms"), Is.Not.Null);
    }

    [Test]
    public void TryRead_ReportsAMissingPlanAsAbsentRatherThanThrowing()
    {
        Assert.That(_store.TryRead("never-written"), Is.Null);
    }

    [Test]
    public void TryRead_RejectsHalfFlushedJsonRatherThanReturningARuinedPlan()
    {
        Write("contact-forms", RealPlan[..80]);

        Assert.That(_store.TryRead("contact-forms"), Is.Null);
    }

    [TestCase("prompt")]
    [TestCase("cwd")]
    [TestCase("writtenAt")]
    public void TryRead_RejectsAPlanMissingAFieldALaunchCannotProceedWithout(string field)
    {
        Write("contact-forms", RealPlan.Replace($"\"{field}\":", "\"unused\":"));

        Assert.That(_store.TryRead("contact-forms"), Is.Null);
    }

    [Test]
    public void TryRead_RejectsAPlanWhosePromptIsPresentButEmpty()
    {
        Write("contact-forms", """
            {"order":1,"title":"t","color":"#000000","cwd":"C:\\dev","addDirs":[],
             "resumeSessionId":null,"writtenAt":"2026-08-25T21:20:33","prompt":"   "}
            """);

        Assert.That(_store.TryRead("contact-forms"), Is.Null);
    }

    [Test]
    public void Keys_ListsEveryPlanInAStableOrder()
    {
        Write("job-queue", RealPlan);
        Write("contact-forms", RealPlan);
        Write("admin-console", RealPlan);

        Assert.That(_store.Keys(), Is.EqualTo(new[] { "admin-console", "contact-forms", "job-queue" }));
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
        var plan = new SessionPlan(2, "Job queue migration", "#D68910", @"C:\work\repo",
            [@"C:\work\repo\JobDispatcher"], "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa",
            "2026-08-26T09:00:00", "Carry on.");

        _store.Write("job-queue", plan);

        var read = _store.TryRead("job-queue");
        Assert.That(read, Is.Not.Null);
        Assert.That(read! with { AddDirs = [], JiraKeys = [] }, Is.EqualTo(plan with { AddDirs = [], JiraKeys = [] }));
        Assert.That(read.AddDirs, Is.EqualTo(plan.AddDirs));
    }

    [Test]
    public void Write_LeavesNoTemporaryFileBehindForTheWatcherToTripOver()
    {
        _store.Write("job-queue", new SessionPlan(1, "t", "#000000", @"C:\dev", [], null,
            "2026-08-26T09:00:00", "Carry on."));

        Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Write_ReplacesAnExistingPlanRatherThanAppendingToIt()
    {
        Write("contact-forms", RealPlan);
        _store.Write("contact-forms", new SessionPlan(9, "Renamed", "#000000", @"C:\dev", [], null,
            "2026-08-26T09:00:00", "New prompt."));

        Assert.That(_store.TryRead("contact-forms")!.Title, Is.EqualTo("Renamed"));
    }

    [Test]
    public void PathFor_NamesTheFileAfterTheKeySoAKeyAndItsPlanCanNeverDisagree()
    {
        Assert.That(_store.PathFor("contact-forms"), Is.EqualTo(Path.Combine(_directory, "contact-forms.json")));
    }

    private void Write(string key, string contents) =>
        File.WriteAllText(Path.Combine(_directory, $"{key}.json"), contents);

    [Test]
    public void TryRead_ReadsJiraKeysASessionAddedToItsOwnPlan()
    {
        Write("contact-forms", """
            {
                "order": 1, "title": "Contact forms", "color": "#C0392B", "cwd": "C:\\work\\repo",
                "addDirs": [], "resumeSessionId": null, "writtenAt": "2026-08-28T09:00:00",
                "prompt": "Pick up the support case.", "jiraKeys": ["NDC-742"]
            }
            """);

        Assert.That(_store.TryRead("contact-forms")!.JiraKeys, Is.EqualTo(new[] { "NDC-742" }));
    }

    [Test]
    public void TryRead_StillReadsAPlanWrittenBeforeJiraKeysExisted()
    {
        Write("contact-forms", RealPlan);

        Assert.That(_store.TryRead("contact-forms")!.JiraKeys, Is.Empty,
            "these files are written by the sessions, so a missing field cannot be an error");
    }

    [Test]
    public void Delete_RemovesThePlanFile()
    {
        Write("contact-forms", RealPlan);

        _store.Delete("contact-forms");

        Assert.That(_store.TryRead("contact-forms"), Is.Null);
        Assert.That(_store.Keys(), Does.Not.Contain("contact-forms"));
    }

    [Test]
    public void Delete_SaysNothingAboutAPlanThatWasNotThere()
    {
        Assert.DoesNotThrow(() => _store.Delete("never-existed"));
    }
}
