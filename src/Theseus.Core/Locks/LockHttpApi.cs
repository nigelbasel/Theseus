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
            "request" => Answer(_book.Request(Asked(request))),
            "leave" => Answer(_book.Leave(Asked(request) with { Mode = HoldMode.Process })),
            "finish" => request["exit"] is JsonValue exit && exit.TryGetValue<int>(out var exitCode)
                ? WithToken(request, token => _book.Finish(token, exitCode))
                : Json(400, Problem("finish needs the command's exit code as exit.")),
            "check" => WithToken(request, _book.Check),
            "release" => WithToken(request, _book.Release),
            "attach" => request["pid"] is JsonValue pid && pid.TryGetValue<int>(out var processId)
                ? WithToken(request, token => _book.Attach(token, processId))
                : Json(400, Problem("attach needs the Windows process id as pid.")),
            _ => Json(404, Problem($"Unknown lock call '{verb}'. Use request, check, attach, finish, release or leave.")),
        };
    }

    private static LockRequest Asked(JsonObject request) => new(
        Text(request, "lock") ?? string.Empty,
        Text(request, "who") ?? string.Empty,
        Text(request, "purpose") ?? string.Empty,
        string.Equals(Text(request, "mode"), "session", StringComparison.OrdinalIgnoreCase) ? HoldMode.Session : HoldMode.Process,
        Text(request, "cwd"),
        Text(request, "sessionId"),
        Text(request, "reason"),
        Scope: ScopeText.Parse(request["scope"]),
        Owner: Text(request, "owner"),
        Card: Text(request, "card"),
        Command: Text(request, "command"),
        Commit: Text(request, "commit"),
        Again: request["again"] is JsonValue again && (again.TryGetValue<bool>(out var flag) ? flag : string.Equals(Text(request, "again"), "true", StringComparison.OrdinalIgnoreCase)),
        Root: Text(request, "root"),
        Since: DateTimeOffset.TryParse(Text(request, "since"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var since) ? since : null);

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
            ["scope"] = answer.Scope is { } scope ? Names(scope) : null,
        };

        return Json(answer.Status == LockStatus.Refused ? 400 : 200, json);
    }

    /// <summary>
    /// Everything held and waiting. <c>locks</c> keeps the shape from before scopes, a row per predefined scope with
    /// the first hold and every waiter that clashes with it, so older clients still read it.
    /// </summary>
    private JsonObject Listing()
    {
        var tickets = _book.Snapshot();
        var table = _book.Table;

        return new JsonObject
        {
            ["now"] = _clock().ToString("o", CultureInfo.InvariantCulture),
            ["locks"] = new JsonArray([.. LockNames.All.Select(name =>
            {
                var scope = PredefinedScopes.Find(name, table)!;
                var clashing = tickets.Where(ticket => !table.Fits(scope, [ticket.Claims])).ToList();

                return (JsonNode?)new JsonObject
                {
                    ["name"] = name,
                    ["guards"] = LockNames.Describe(name),
                    ["holder"] = clashing.FirstOrDefault(ticket => ticket.IsHolder) is { } holder ? Public(holder) : null,
                    ["waiting"] = new JsonArray([.. clashing.Where(ticket => !ticket.IsHolder).Select(ticket => (JsonNode?)Public(ticket))]),
                };
            })]),
            ["tickets"] = new JsonArray([.. tickets.Select(ticket => (JsonNode?)Public(ticket))]),
            ["resources"] = new JsonArray([.. ResourceNames(tickets, table).Select(name => (JsonNode?)new JsonObject
            {
                ["name"] = name,
                ["capacity"] = table.Capacity(name),
                ["known"] = table.IsKnown(name),
                ["holders"] = Using(tickets.Where(ticket => ticket.IsHolder), name),
                ["waiting"] = Using(tickets.Where(ticket => !ticket.IsHolder), name),
            })]),
            ["deadlocks"] = new JsonArray([.. _book.StoppedDeadlocks().Reverse().Select(stopped => (JsonNode?)new JsonObject
            {
                ["when"] = stopped.WhenUtc.ToString("o", CultureInfo.InvariantCulture),
                ["owners"] = new JsonArray([.. stopped.Owners.Select(owner => (JsonNode?)owner)]),
                ["resources"] = new JsonArray([.. stopped.Resources.Select(resource => (JsonNode?)resource)]),
                ["detail"] = stopped.Detail,
            })]),
            ["runs"] = new JsonArray([.. _book.RecentRuns().Reverse().Select(run => (JsonNode?)new JsonObject
            {
                ["card"] = run.Card,
                ["commit"] = run.Commit,
                ["command"] = run.Command,
                ["exit"] = run.ExitCode,
                ["when"] = run.WhenUtc.ToString("o", CultureInfo.InvariantCulture),
            })]),
            ["rules"] = new JsonArray([.. table.Rules.Select(rule => (JsonNode?)new JsonObject
            {
                ["name"] = rule.Name,
                ["capacity"] = rule.Capacity,
                ["clashesWith"] = new JsonArray([.. rule.ClashesWith.Select(other => (JsonNode?)other)]),
                ["why"] = rule.Why,
                ["ruledBy"] = rule.RuledBy,
            })]),
        };
    }

    private static IEnumerable<string> ResourceNames(IReadOnlyList<LockTicket> tickets, ResourceTable table) =>
        table.Rules.Where(rule => !rule.Name.EndsWith(":*", StringComparison.Ordinal)).Select(rule => rule.Name)
            .Concat(tickets.SelectMany(ticket => ticket.Claims).Select(claim => claim.Resource))
            .Distinct(StringComparer.Ordinal);

    private static JsonArray Using(IEnumerable<LockTicket> tickets, string resource) =>
        new([.. tickets
            .Select(ticket => (Ticket: ticket, Claim: ticket.Claims.FirstOrDefault(claim => claim.Resource == resource)))
            .Where(entry => entry.Claim is not null)
            .Select(entry => (JsonNode?)new JsonObject
            {
                ["who"] = entry.Ticket.Who,
                ["purpose"] = entry.Ticket.Purpose,
                ["amount"] = entry.Claim!.Amount,
            })]);

    private static JsonArray Names(IEnumerable<ResourceClaim> scope) =>
        new([.. scope.Select(claim => (JsonNode?)claim.ToString())]);

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
        ["scope"] = Names(ticket.Claims),
        ["card"] = ticket.Card,
        ["commit"] = ticket.Commit,
        ["command"] = ticket.Command,
        ["overtaken"] = ticket.Overtaken,
    };

    private static JsonObject Problem(string detail) =>
        new() { ["status"] = "refused", ["detail"] = detail };

    private static McpResponse Json(int status, JsonObject body) => new(status, body.ToJsonString());

    private static string? Text(JsonObject request, string name) =>
        request[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
