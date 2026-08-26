using Theseus.Core.Domain;
using Theseus.Core.Persistence;

namespace Theseus.Tests.Persistence;

[TestFixture]
public class BoardStoreTests
{
    private string _directory = string.Empty;
    private string _path = string.Empty;
    private BoardStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-board-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "board.json");
        _store = new BoardStore(_path);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Read_IsEmptyBeforeTheFirstBoardIsEverWritten()
    {
        Assert.That(_store.Read(), Is.Empty);
    }

    [Test]
    public void Write_RoundTripsEverySidecarFieldIncludingSeveralJiraKeys()
    {
        var sidecar = Sidecar("gdpr-mcp") with { JiraKeys = ["APPS-2170", "NDC-202"] };

        _store.Write([sidecar]);

        var read = _store.Read().Single();
        Assert.That(read with { JiraKeys = [] }, Is.EqualTo(sidecar with { JiraKeys = [] }));
        Assert.That(read.JiraKeys, Is.EqualTo(new[] { "APPS-2170", "NDC-202" }));
    }

    [Test]
    public void Write_StoresColumnsByNameSoTheFileSurvivesReorderingTheEnum()
    {
        _store.Write([Sidecar("gdpr-mcp") with { Column = BoardColumn.Parked }]);

        Assert.That(File.ReadAllText(_path), Does.Contain("Parked"));
    }

    [Test]
    public void Write_ReplacesTheWholeFileRatherThanMergingIntoIt()
    {
        _store.Write([Sidecar("gdpr-mcp"), Sidecar("legacy-jobs")]);
        _store.Write([Sidecar("gdpr-mcp")]);

        Assert.That(_store.Read().Select(s => s.Key), Is.EqualTo(new[] { "gdpr-mcp" }));
    }

    [Test]
    public void Write_LeavesNoTemporaryFileBehind()
    {
        _store.Write([Sidecar("gdpr-mcp")]);

        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Read_FallsBackToAnEmptyBoardRatherThanCrashingOnACorruptedFile()
    {
        File.WriteAllText(_path, "{ this is not the board");

        Assert.That(_store.Read(), Is.Empty);
    }

    [Test]
    public void Write_CreatesTheDirectoryOnFirstUse()
    {
        var nested = Path.Combine(_directory, "Theseus", "board.json");
        new BoardStore(nested).Write([Sidecar("gdpr-mcp")]);

        Assert.That(File.Exists(nested), Is.True);
    }

    private static WorkstreamSidecar Sidecar(string key) =>
        new(key, BoardColumn.Today, IsBlocked: false, IsArchived: false, ColorIsManual: true,
            ReopenTomorrow: true, ["APPS-2170"], @"C:\dev\tfsgit\SESSION-STATUS-gdpr-mcp.md",
            "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa",
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 26, 9, 30, 0, TimeSpan.Zero));
}
