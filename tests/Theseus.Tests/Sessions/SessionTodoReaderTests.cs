using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class SessionTodoReaderTests
{
    private const string SessionId = "7d73cb6d-9784-4167-9235-8d84008e3500";

    private string _root = string.Empty;
    private SessionTodoReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-todos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, SessionId));
        _reader = new SessionTodoReader(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Read_ReadsAListInTheShapeClaudeCodeWritesIt()
    {
        Write("1", """
            {
              "id": "1",
              "subject": "Port Core TextRenderingTests (17) to NDC",
              "description": "Triage then port.",
              "activeForm": "Porting TextRenderingTests",
              "status": "in_progress",
              "blocks": [],
              "blockedBy": []
            }
            """);

        var todo = _reader.Read(SessionId).Single();

        Assert.That(todo.Subject, Is.EqualTo("Port Core TextRenderingTests (17) to NDC"));
        Assert.That(todo.ActiveForm, Is.EqualTo("Porting TextRenderingTests"));
        Assert.That(todo.IsInProgress, Is.True);
    }

    [Test]
    public void Read_OrdersTheListTheWayTheSessionNumberedItRatherThanAlphabetically()
    {
        foreach (var id in new[] { "1", "2", "10" })
        {
            Write(id, $$"""{"id":"{{id}}","subject":"Item {{id}}","status":"pending"}""");
        }

        Assert.That(_reader.Read(SessionId).Select(todo => todo.Id),
            Is.EqualTo(new[] { "1", "2", "10" }));
    }

    [Test]
    public void Read_SkipsAnItemBeingWrittenRatherThanLosingTheWholeList()
    {
        Write("1", """{"id":"1","subject":"Good","status":"pending"}""");
        Write("2", "{ half a file");

        Assert.That(_reader.Read(SessionId).Select(todo => todo.Subject), Is.EqualTo(new[] { "Good" }));
    }

    [Test]
    public void Read_FindsNothingForASessionThatKeepsNoList()
    {
        Assert.That(_reader.Read("11111111-2222-3333-4444-555555555555"), Is.Empty);
    }

    [Test]
    public void Read_FindsNothingWhenThereIsNoSessionToAskAbout()
    {
        Assert.That(_reader.Read(null), Is.Empty);
        Assert.That(_reader.Read("  "), Is.Empty);
    }

    [Test]
    public void Read_IgnoresTheLockFileClaudeCodeKeepsBesideTheItems()
    {
        Write("1", """{"id":"1","subject":"Good","status":"pending"}""");
        File.WriteAllText(Path.Combine(_root, SessionId, ".lock"), string.Empty);

        Assert.That(_reader.Read(SessionId), Has.Count.EqualTo(1));
    }

    private void Write(string id, string json) =>
        File.WriteAllText(Path.Combine(_root, SessionId, $"{id}.json"), json);
}
