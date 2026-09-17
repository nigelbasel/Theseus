using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Theseus.App.Services;
using Theseus.Core.Attention;
using Theseus.Core.Mcp;

namespace Theseus.Tests.Mcp;

/// <summary>The session time server on a real localhost port, the way a session reaches it.</summary>
[TestFixture]
public class McpHttpServerTests
{
    private string _directory = string.Empty;
    private McpHttpServer _server = null!;
    private HttpClient _client = null!;
    private string _url = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-http-{Guid.NewGuid():N}");

        var handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => [],
            sessionId => new McpSession(sessionId, "gdpr-mcp", "GDPR MCP"),
            () => DateTimeOffset.UtcNow);

        var port = FreePort();

        _server = new McpHttpServer(handler, port);
        Assert.That(_server.Start(), Is.True, "the server could not listen on localhost");

        _client = new HttpClient();
        _url = TheseusMcp.UrlFor(port, "session-1");
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _server.DisposeAsync();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task Post_AnswersInitializeWithOneJsonObject()
    {
        using var response = await Post("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}""");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        Assert.That(body["result"]!["protocolVersion"]!.GetValue<string>(), Is.EqualTo("2025-06-18"));
    }

    [Test]
    public async Task Post_AcceptsANotificationWith202()
    {
        using var response = await Post("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
    }

    [Test]
    public async Task Get_IsRefusedBecauseThereIsNoEventStream()
    {
        using var response = await _client.GetAsync(_url);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }

    [Test]
    public async Task Post_FromAWebPageOnAnotherOrigin_IsRefused()
    {
        using var response = await Post("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", origin: "http://evil.example");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private async Task<HttpResponseMessage> Post(string json, string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await _client.SendAsync(request);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
}
