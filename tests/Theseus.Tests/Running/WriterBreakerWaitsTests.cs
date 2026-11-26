using Theseus.Core.Breakers;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Core.Running;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Running;

/// <summary>
/// A card Theseus made that its breaker stopped waits for the owner's Reset in Waits for you, whatever its role (Nigel,
/// 2026-11-26: "no sign of notifications to restart the breakers"). A scenario writer is named by no project file, so its
/// card carries no project: Waits for you took only cards that did, and the writer's Reset breaker line never showed.
/// </summary>
[TestFixture]
public class WriterBreakerWaitsTests
{
    private static readonly BreakerTrip Tripped =
        new(BreakerKind.NoProgress, "Breaker tripped: 3 runs in a row with no new commit, record, tick or report.", DateTimeOffset.UnixEpoch);

    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static Workstream Card(string key, string title, WorkstreamRole role, string cwd, IReadOnlyList<string> addDirs) =>
        new(key, title, "#03999F", ColorIsManual: true, cwd, addDirs, "Carry on.", 1, "New",
            IsBlocked: false, ReopenTomorrow: false, IsArchived: false, LastSessionId: null, [], null, DateTimeOffset.UnixEpoch)
        {
            Role = role,
            Breaker = Tripped,
            IsPaused = true,
            PausedBy = "breaker",
        };

    private Workstream Writer(string? project = null) =>
        Card("write-scenarios-for-one", "Write scenarios for One", WorkstreamRole.ScenarioWriter, @"C:\scenarios\one", [_folder.Home(OneProject)])
            with { Project = project };

    private IReadOnlyList<ProjectFileReading> Readings()
    {
        WriteOne(_folder, "feature");

        return ProjectCatalog.Read(_folder.Folder);
    }

    [Test]
    public void AWriterNamedByNoProjectFile_AnswersToTheProjectWhoseHomeItHasInReach()
    {
        Assert.That(RoleProject.Of(Writer(), Readings()), Is.EqualTo(OneProject));
    }

    [Test]
    public void AWriterMadeWithItsProject_KeepsIt()
    {
        Assert.That(RoleProject.Of(Writer("another"), Readings()), Is.EqualTo("another"));
    }

    [Test]
    public void ACardWithNoRole_AndNoProject_AnswersToNone()
    {
        var adhoc = Card("adhoc", "Ad hoc", WorkstreamRole.None, @"C:\work", [_folder.Home(OneProject)]);

        Assert.That(RoleProject.Of(adhoc, Readings()), Is.Null, "only a writer is placed by the folders in its reach");
    }

    [Test]
    public void AWritersTrippedBreaker_IsALineOfItsProjectsWaitsForYou_WithResetBreaker()
    {
        var writer = Writer();

        var line = HeadlessWaits.ForProject(OneProject, [writer], Readings(), _ => null).Single();

        Assert.Multiple(() =>
        {
            Assert.That(line.Kind, Is.EqualTo(WaitKind.ResetBreaker));
            Assert.That(line.CardKey, Is.EqualTo(writer.Key));
            Assert.That(line.Button, Is.EqualTo("Reset breaker"));
            Assert.That(line.Text, Does.StartWith("Write scenarios for One:"));
        });
    }

    [Test]
    public void AVerifiersTrippedBreaker_IsALineOfItsProjectsWaitsForYou_WithResetBreaker()
    {
        // A verifier takes its subject's project as the board reads the links.
        var verifier = Card("verify-a-piece", "Verify: a piece", WorkstreamRole.Verifier, @"C:\scenarios\one", []) with { Project = OneProject };

        Assert.That(HeadlessWaits.ForProject(OneProject, [verifier], Readings(), _ => null).Single().Kind, Is.EqualTo(WaitKind.ResetBreaker));
    }

    [Test]
    public void AnotherProjectsWriter_IsNotThisProjectsWait()
    {
        Assert.That(HeadlessWaits.ForProject("another", [Writer()], Readings(), _ => null), Is.Empty);
    }
}
