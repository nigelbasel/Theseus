using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Theseus.Core.Mcp;

namespace Theseus.Core.Locks;

/// <summary>
/// The lock book over plain HTTP, for the wrapper script and the guard hook, which cannot call MCP tools
/// (Nigel, 2026-10-06). Every call is a POST of one JSON object to <c>/locks/&lt;verb&gt;</c>, and a GET of
/// <c>/locks/</c> lists what is held and waiting.
///
/// A token is only ever returned to the request that created it, never in a listing, so only the
/// requester can attach or release. Reordering, cancelling and releasing someone else's lock are not
/// offered here at all: they belong to the board.
/// </summary>
public sealed class LockHttpApi
{
    private readonly LockBook _book;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>Creates the API over a lock book.</summary>
    public LockHttpApi(LockBook book, Func<DateTimeOffset> clock)
    {
        _book = book;
        _clock = clock;
    }

    /// <summary>Answers one request. <paramref name="verb"/> is the path after <c>/locks/</c>.</summary>
    public McpResponse Handle(string method, string verb, string body)
    {
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) && verb.Length == 0)
        {
            return Json(200, Listing());
        }

        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return Json(405, Problem("Use POST, or GET /locks/ for the listing."));
        }

        JsonObject request;

        try
        {
            request = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return Json(400, Problem("The body is not a JSON object."));
        }

        return verb switch
        {
            "request" => Answer(_book.Request(new LockRequest(
                Text(request, "lock") ?? string.Empty,
                Text(request, "who") ?? string.Empty,
                Text(request, "purpose") ?? string.Empty,
                string.Equals(Text(request, "mode"), "session", StringComparison.OrdinalIgnoreCase) ? HoldMode.Session : HoldMode.Process,
                Text(request, "cwd"),
                Text(request, "sessionId"),
                Text(request, "reason")))),
            "check" => WithToken(request, _book.Check),
            "release" => WithToken(request, _book.Release),
            "attach" => request["pid"] is JsonValue pid && pid.TryGetValue<int>(out var processId)
                ? WithToken(request, token => _book.Attach(token, processId))
                : Json(400, Problem("attach needs the Windows process id as pid.")),
            _ => Json(404, Problem($"Unknown lock call '{verb}'. Use request, check, attach or release.")),
        };
    }

    private McpResponse WithToken(JsonObject request, Func<string, LockAnswer> call) =>
        Text(request, "token") is { } token
            ? Answer(call(token))
            : Json(400, Problem("This call needs the token the request returned."));

    private McpResponse Answer(LockAnswer answer)
    {
        var json = new JsonObject
        {
            ["status"] = answer.Status.ToString().ToLowerInvariant(),
            ["detail"] = answer.Detail,
            ["token"] = answer.Token,
            ["lock"] = answer.Lock,
            ["position"] = answer.Position,
            ["reservedUntil"] = answer.ReservedUntilUtc?.ToString("o", CultureInfo.InvariantCulture),
            ["holder"] = answer.Holder is { } holder ? Public(holder) : null,
        };

        return Json(answer.Status == LockStatus.Refused ? 400 : 200, json);
    }

    private JsonObject Listing()
    {
        var tickets = _book.Snapshot();

        return new JsonObject
        {
            ["now"] = _clock().ToString("o", CultureInfo.InvariantCulture),
            ["locks"] = new JsonArray([.. LockNames.All.Select(name => (JsonNode?)new JsonObject
            {
                ["name"] = name,
                ["guards"] = LockNames.Describe(name),
                ["holder"] = tickets.FirstOrDefault(ticket => ticket.IsHolder && ticket.Lock == name) is { } holder ? Public(holder) : null,
                ["waiting"] = new JsonArray([.. tickets
                    .Where(ticket => !ticket.IsHolder && ticket.Lock == name)
                    .Select(ticket => (JsonNode?)Public(ticket))]),
            })]),
        };
    }

    private static JsonObject Public(LockTicket ticket) => new()
    {
        ["who"] = ticket.Who,
        ["purpose"] = ticket.Purpose,
        ["mode"] = ticket.Mode.ToString().ToLowerInvariant(),
        ["requested"] = ticket.RequestedUtc.ToString("o", CultureInfo.InvariantCulture),
        ["granted"] = ticket.GrantedUtc?.ToString("o", CultureInfo.InvariantCulture),
        ["pid"] = ticket.ProcessId,
        ["cwd"] = ticket.WorkingDirectory,
        ["reason"] = ticket.Reason,
    };

    private static JsonObject Problem(string detail) =>
        new() { ["status"] = "refused", ["detail"] = detail };

    private static McpResponse Json(int status, JsonObject body) => new(status, body.ToJsonString());

    private static string? Text(JsonObject request, string name) =>
        request[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
