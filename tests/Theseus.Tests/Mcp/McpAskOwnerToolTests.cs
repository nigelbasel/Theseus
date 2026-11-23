using System.Text.Json.Nodes;
using Theseus.Core.Attention;
using Theseus.Core.Mcp;
using Theseus.Core.Questions;

namespace Theseus.Tests.Mcp;

/// <summary>ask_owner (1.6 spec items 3 and 4), through the server as a session calls it.</summary>
[TestFixture]
public class McpAskOwnerToolTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private OwnerQuestionStore _store = null!;
    private int _changes;
    private McpHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-ask-{Guid.NewGuid():N}");
        _store = new OwnerQuestionStore(Path.Combine(_directory, "questions"));
        _changes = 0;
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => sessionId == SessionId ? new McpSession(SessionId, "ledger-mcp", "LEDGER MCP") : null,
            () => Nine,
            askOwner: new AskOwnerTool(_store, () => Nine, () => _changes++));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void AQuestion_IsKeptForTheCallingSession_AndTheBoardIsTold()
    {
        var result = Call(new JsonObject
        {
            ["question"] = "Push the branch now?",
            ["options"] = new JsonArray("Yes", "Later"),
            ["why"] = "CI is idle",
        });

        var question = _store.Read(SessionId)!;

        Assert.That(result["isError"]!.GetValue<bool>(), Is.False);
        Assert.That((question.Text, question.IsGuess, question.Why, question.AskedUtc), Is.EqualTo(("Push the branch now?", false, "CI is idle", Nine)));
        Assert.That(question.Options, Is.EqualTo(new[] { "Yes", "Later" }));
        Assert.That(_changes, Is.EqualTo(1));
    }

    [Test]
    public void TheReply_TellsTheSessionToAskInItsTabToo()
    {
        Assert.That(Text(Call(new JsonObject { ["question"] = "Push now?" })), Does.Contain("question tool"));
    }

    [Test]
    public void Answered_ClearsTheQuestion()
    {
        Call(new JsonObject { ["question"] = "Push now?" });

        Call(new JsonObject { ["answered"] = true });

        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [Test]
    public void AQuestionOverSeveralLines_IsShownAsOne()
    {
        Call(new JsonObject { ["question"] = "Push\r\n the branch\nnow?" });

        Assert.That(_store.Read(SessionId)!.Text, Is.EqualTo("Push the branch now?"));
    }

    [TestCase(null)]
    [TestCase("   ")]
    public void NoQuestion_IsRefused(string? question)
    {
        var arguments = new JsonObject();

        if (question is not null)
        {
            arguments["question"] = question;
        }

        Assert.That(Call(arguments)["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [Test]
    public void AnOverLongQuestion_OrTooManyOptions_IsRefused()
    {
        Assert.That(Call(new JsonObject { ["question"] = new string('x', ProseQuestion.MaxLength + 1) })["isError"]!.GetValue<bool>(), Is.True);
        Assert.That(Call(new JsonObject { ["question"] = "Which?", ["options"] = new JsonArray("a", "b", "c", "d", "e") })["isError"]!.GetValue<bool>(),
            Is.True);
        Assert.That(_store.Read(SessionId), Is.Null);
    }

    [Test]
    public void TheToolIsListed_AndApprovedInAdvance()
    {
        var response = _handler.Handle(SessionId,
            new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/list" }.ToJsonString(), null);
        var names = JsonNode.Parse(response.Body!)!["result"]!["tools"]!.AsArray().Select(tool => tool!["name"]!.GetValue<string>());

        Assert.That(names, Does.Contain(AskOwnerTool.Name));
        Assert.That(TheseusMcp.PreApproved, Does.Contain(AskOwnerTool.Name), "asking the owner changes nothing on the board he has to approve");
    }

    private JsonNode Call(JsonObject arguments)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = AskOwnerTool.Name, ["arguments"] = arguments },
        };

        return JsonNode.Parse(_handler.Handle(SessionId, message.ToJsonString(), null).Body!)!["result"]!;
    }

    private static string Text(JsonNode result) => result["content"]![0]!["text"]!.GetValue<string>();
}
