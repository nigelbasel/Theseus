using System.Text.Json.Nodes;
using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class ScopedHttpApiTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private HttpClock _clock = null!;
    private LockBook _book = null!;
    private LockHttpApi _api = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-scoped-api-{Guid.NewGuid():N}");
        _clock = new HttpClock(Nine);
        _book = new LockBook(
            new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            new HttpLiveness(),
            _clock);
        _api = new LockHttpApi(_book, () => _clock.UtcNow);
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
    public void ARequestNamingAScope_InEitherForm_IsGrantedBesideOneThatDoesNotClash()
    {
        JsonObject first = Post("request", """{"who":"a","purpose":"stack","scope":["compose:members-hub","port:8080"]}""");
        JsonObject second = Post("request", """{"who":"b","purpose":"stack","scope":[{"resource":"compose:leadpro","amount":1}]}""");

        Assert.That(first["status"]!.GetValue<string>(), Is.EqualTo("granted"));
        Assert.That(second["status"]!.GetValue<string>(), Is.EqualTo("granted"));
        Assert.That(first["scope"]!.AsArray().Select(node => node!.GetValue<string>()), Is.EqualTo(new[] { "compose:members-hub", "port:8080" }));
    }

    [Test]
    public void ACommandWithItsTree_IsHeldForItsLeastScope()
    {
        JsonObject build = Post("request", """{"who":"a","purpose":"build","command":"dotnet build Fabrikam.sln","root":"E:\\work\\x\\Fabrikam","cwd":"E:\\work\\x\\Fabrikam\\src"}""");

        Assert.That(build["scope"]!.AsArray().Select(node => node!.GetValue<string>()), Is.EqualTo(new[] { "cpu-build", "worktree:e:/work/x/fabrikam" }));
    }

    [Test]
    public void TheSameRunTwice_IsRefusedWith400_AndAgainQueuesIt()
    {
        string body = """{"who":"nuf-1","purpose":"suite","card":"nuf-1","commit":"abc","command":"dotnet test Fabrikam.PersistenceTests --no-build","root":"E:\\work\\x"}""";
        Post("request", body);

        McpResponseShape refused = Raw("request", body.Replace("\"purpose\":\"suite\"", "\"purpose\":\"suite again\"", StringComparison.Ordinal));
        JsonObject again = Post("request", body.Replace("\"purpose\":\"suite\"", "\"purpose\":\"flaky\",\"again\":true", StringComparison.Ordinal));

        Assert.That(refused.Status, Is.EqualTo(400));
        Assert.That(again["status"]!.GetValue<string>(), Is.EqualTo("waiting"));
    }

    [Test]
    public void Finish_RecordsTheExitCode_AndTheListingShowsIt()
    {
        string token = Post("request", """{"who":"a","purpose":"suite","card":"nuf-1","commit":"abc","command":"dotnet test X.Api.Tests --no-build"}""")["token"]!.GetValue<string>();

        JsonObject finished = Post("finish", $$"""{"token":"{{token}}","exit":2}""");
        JsonObject listing = Listing();

        Assert.That(finished["status"]!.GetValue<string>(), Is.EqualTo("ended"));
        Assert.That(listing["runs"]![0]!["exit"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(listing["runs"]![0]!["commit"]!.GetValue<string>(), Is.EqualTo("abc"));
    }

    [Test]
    public void TheListing_ShowsEveryTicketsScope_ResourceRows_AndTheOldLockRows()
    {
        Post("request", """{"who":"suite","purpose":"persistence","scope":["sql-containers"]}""");
        Post("request", """{"who":"old","purpose":"everything","lock":"tests"}""");

        JsonObject listing = Listing();
        JsonObject tests = listing["locks"]!.AsArray().Single(node => node!["name"]!.GetValue<string>() == "tests")!.AsObject();
        JsonObject sql = listing["resources"]!.AsArray().Single(node => node!["name"]!.GetValue<string>() == "sql-containers")!.AsObject();

        Assert.That(listing["tickets"]!.AsArray(), Has.Count.EqualTo(2));
        Assert.That(tests["holder"]!["who"]!.GetValue<string>(), Is.EqualTo("suite"));
        Assert.That(tests["waiting"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(sql["capacity"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(sql["holders"]![0]!["who"]!.GetValue<string>(), Is.EqualTo("suite"));
        Assert.That(sql["waiting"]![0]!["who"]!.GetValue<string>(), Is.EqualTo("old"));
    }

    [Test]
    public void ALaterStepOfASequence_KeepsTheSequencesPlace()
    {
        Post("request", """{"who":"holder","purpose":"suite","scope":["sql-containers"]}""");
        _clock.Advance(TimeSpan.FromMinutes(1));
        DateTimeOffset sequenceStart = _clock.UtcNow;
        _clock.Advance(TimeSpan.FromMinutes(1));
        Post("request", """{"who":"later","purpose":"suite","scope":["sql-containers"]}""");
        _clock.Advance(TimeSpan.FromMinutes(1));

        JsonObject step = Post("request", $$"""{"who":"steps","purpose":"step 2","scope":["sql-containers"],"since":"{{sequenceStart:o}}"}""");

        Assert.That(step["position"]!.GetValue<int>(), Is.EqualTo(1));
    }

    [Test]
    public void AStoppedDeadlock_IsInTheListing()
    {
        Post("request", """{"who":"session A","purpose":"stack","lock":"dev-stack","mode":"session","sessionId":"A"}""");
        Post("request", """{"who":"session B","purpose":"suite","scope":["sql-containers"],"owner":"B"}""");
        Post("request", """{"who":"session A","purpose":"suite","scope":["sql-containers"],"owner":"A"}""");

        McpResponseShape refused = Raw("request", """{"who":"session B","purpose":"stack","scope":["dev-stack"],"owner":"B"}""");

        Assert.That(refused.Status, Is.EqualTo(400));
        Assert.That(Listing()["deadlocks"]!.AsArray(), Has.Count.EqualTo(1));
    }

    private JsonObject Post(string verb, string body) => JsonNode.Parse(_api.Handle("POST", verb, body).Body!)!.AsObject();

    private McpResponseShape Raw(string verb, string body)
    {
        Theseus.Core.Mcp.McpResponse response = _api.Handle("POST", verb, body);

        return new McpResponseShape(response.StatusCode, response.Body ?? string.Empty);
    }

    private JsonObject Listing() => JsonNode.Parse(_api.Handle("GET", string.Empty, string.Empty).Body!)!.AsObject();

    private sealed record McpResponseShape(int Status, string Body);

    private sealed class HttpClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan span) => UtcNow += span;
    }

    private sealed class HttpLiveness : ILockLiveness
    {
        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => true;
    }
}
