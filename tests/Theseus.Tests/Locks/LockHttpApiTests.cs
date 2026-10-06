using System.Text.Json.Nodes;
using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks;

[TestFixture]
public class LockHttpApiTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private FakeLiveness _liveness = null!;
    private LockHttpApi _api = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-lock-api-{Guid.NewGuid():N}");
        _liveness = new FakeLiveness();

        var book = new LockBook(
            new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            _liveness,
            new FixedClock(Nine));

        _api = new LockHttpApi(book, () => Nine);
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
    public void Request_ReturnsTheTokenAndStanding()
    {
        var answer = Post("request", """{"lock":"tests","who":"Legacy job migration","purpose":"PersistenceTests.Jobs","cwd":"C:\\dev\\tfsgit"}""");

        Assert.That(answer["status"]!.GetValue<string>(), Is.EqualTo("granted"));
        Assert.That(answer["token"]!.GetValue<string>(), Has.Length.EqualTo(32));
    }

    [Test]
    public void Request_ForAnUnknownLock_IsA400()
    {
        var response = _api.Handle("POST", "request", """{"lock":"sql","who":"a","purpose":"b"}""");

        Assert.That(response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void AttachCheckAndRelease_WorkWithTheToken()
    {
        var token = Post("request", """{"lock":"tests","who":"a","purpose":"b"}""")["token"]!.GetValue<string>();
        _liveness.Processes[4242] = "start";

        Assert.That(Post("attach", $$"""{"token":"{{token}}","pid":4242}""")["status"]!.GetValue<string>(), Is.EqualTo("granted"));
        Assert.That(Post("check", $$"""{"token":"{{token}}"}""")["status"]!.GetValue<string>(), Is.EqualTo("granted"));
        Assert.That(Post("release", $$"""{"token":"{{token}}"}""")["status"]!.GetValue<string>(), Is.EqualTo("ended"));
    }

    [Test]
    public void Listing_ShowsHolderAndQueueButNeverATokenInIt()
    {
        var token = Post("request", """{"lock":"tests","who":"Legacy job migration","purpose":"PersistenceTests.Jobs"}""")["token"]!.GetValue<string>();
        Post("request", """{"lock":"tests","who":"NDC-1190","purpose":"Api.Contacts.Tests","reason":"blocking a release"}""");

        var response = _api.Handle("GET", string.Empty, string.Empty);
        var tests = JsonNode.Parse(response.Body!)!["locks"]!.AsArray().First(node => node!["name"]!.GetValue<string>() == "tests")!;

        Assert.That(tests["holder"]!["who"]!.GetValue<string>(), Is.EqualTo("Legacy job migration"));
        Assert.That(tests["waiting"]!.AsArray()[0]!["reason"]!.GetValue<string>(), Is.EqualTo("blocking a release"));
        Assert.That(response.Body, Does.Not.Contain(token));
    }

    [Test]
    public void ThereIsNoCallToReorderOrCancelOrForceRelease()
    {
        foreach (var verb in new[] { "reorder", "cancel", "force-release" })
        {
            Assert.That(_api.Handle("POST", verb, "{}").StatusCode, Is.EqualTo(404), verb);
        }
    }

    [Test]
    public void ABadBody_IsA400()
    {
        Assert.That(_api.Handle("POST", "check", "not json").StatusCode, Is.EqualTo(400));
        Assert.That(_api.Handle("POST", "check", "{}").StatusCode, Is.EqualTo(400));
    }

    private JsonNode Post(string verb, string body) => JsonNode.Parse(_api.Handle("POST", verb, body).Body!)!;

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FakeLiveness : ILockLiveness
    {
        public Dictionary<int, string> Processes { get; } = [];

        public string? StartOf(int processId) => Processes.GetValueOrDefault(processId);

        public bool IsSessionAlive(string sessionId) => true;
    }
}
