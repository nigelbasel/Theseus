using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Theseus.Core.Attention;

namespace Theseus.Core.Mcp;

/// <summary>
/// The session time server's protocol: MCP over JSON-RPC, as the Streamable HTTP transport carries it,
/// answering every request with a single JSON object and no event stream.
///
/// Four tools, deliberately small. A session says what it is working on with <c>start_work</c> and
/// <c>stop_work</c>, and asks for the total with <c>time_for</c> when it logs time. It never sends a
/// duration: the time comes from the attention sampler, attended and machine separately, and the
/// person logging decides which to use (Nigel, 2026-09-17). Theseus still never writes to Jira.
/// </summary>
public sealed partial class McpHandler
{
    /// <summary>The protocol versions understood, newest first.</summary>
    public static readonly IReadOnlyList<string> SupportedVersions = ["2025-06-18", "2025-03-26", "2024-11-05"];

    /// <summary>What sessions are told about the server when they connect.</summary>
    public const string Instructions =
        "Theseus launched this session and measures its time; you only say what you are working on. "
        + "When you begin work on a Jira issue, call start_work with its key. Call start_work again when you "
        + "move to a different issue, and stop_work when you stop working on issues. Never estimate, report or "
        + "log a duration of your own. When asked to log time to Jira, call time_for for the issue: it returns "
        + "attended time (the user at this work's tab) and machine time (sessions working) separately. Show the "
        + "user both, let them choose which to log, and get their approval before writing any worklog.";

    private readonly WorkLog _work;
    private readonly Func<IReadOnlyList<AttentionInterval>> _attention;
    private readonly Func<string, McpSession?> _sessions;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeZoneInfo _zone;

    /// <summary>Creates the handler over the work log, the sampler's intervals and the launched sessions.</summary>
    /// <param name="sessions">Finds the session a request's path names, or null for one Theseus did not launch.</param>
    /// <param name="zone">The time zone a bare date means midnight in. Defaults to this machine's.</param>
    public McpHandler(
        WorkLog work,
        Func<IReadOnlyList<AttentionInterval>> attention,
        Func<string, McpSession?> sessions,
        Func<DateTimeOffset> clock,
        TimeZoneInfo? zone = null)
    {
        _work = work;
        _attention = attention;
        _sessions = sessions;
        _clock = clock;
        _zone = zone ?? TimeZoneInfo.Local;
    }

    /// <summary>Answers one POSTed JSON-RPC message for the session its path names.</summary>
    public McpResponse Handle(string sessionId, string body, string? protocolVersionHeader)
    {
        if (!string.IsNullOrWhiteSpace(protocolVersionHeader) && !SupportedVersions.Contains(protocolVersionHeader.Trim()))
        {
            return new McpResponse(400, Error(null, -32600, $"Unsupported MCP-Protocol-Version: {protocolVersionHeader}"));
        }

        JsonNode? message;

        try
        {
            message = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return new McpResponse(400, Error(null, -32700, "Parse error"));
        }

        if (message is not JsonObject request)
        {
            return new McpResponse(400, Error(null, -32600, "Expected a single JSON-RPC message"));
        }

        var method = Text(request["method"]);

        if (method is null || request["id"] is not { } requestId)
        {
            return new McpResponse(202, null);
        }

        var id = requestId.DeepClone();

        return method switch
        {
            "initialize" => Ok(id, Initialize(request["params"] as JsonObject)),
            "ping" => Ok(id, new JsonObject()),
            "tools/list" => Ok(id, ToolsList()),
            "tools/call" => CallTool(id, sessionId, request["params"] as JsonObject),
            _ => new McpResponse(200, Error(id, -32601, $"Method not found: {method}")),
        };
    }

    private static JsonObject Initialize(JsonObject? parameters)
    {
        var requested = Text(parameters?["protocolVersion"]);

        return new JsonObject
        {
            ["protocolVersion"] = requested is not null && SupportedVersions.Contains(requested) ? requested : SupportedVersions[0],
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = TheseusMcp.ServerName, ["title"] = "Theseus", ["version"] = "1.0.0" },
            ["instructions"] = Instructions,
        };
    }

    private static JsonObject ToolsList() => new()
    {
        ["tools"] = new JsonArray(
            Tool("start_work",
                "Say that this session is now working on a Jira issue. Theseus measures the time from here; never send a duration. Call again for a different issue.",
                new JsonObject
                {
                    ["issue"] = Property("Jira issue key, for example NDC-680."),
                    ["summary"] = Property("One line on what you are doing."),
                },
                "issue"),
            Tool("stop_work",
                "Say that this session has stopped working on issues.",
                new JsonObject()),
            Tool("time_for",
                "The time spent on a Jira issue, as attended time and machine time, never added together. Use when logging time: show the user both and let them choose.",
                new JsonObject
                {
                    ["issue"] = Property("Jira issue key, for example NDC-680."),
                    ["from"] = Property("Start, as an ISO 8601 date or date-time. A date means midnight. Defaults to the start of today."),
                    ["to"] = Property("End, as an ISO 8601 date or date-time. Defaults to now."),
                },
                "issue"),
            Tool("whoami",
                "Which workstream this session is, and the issue it is currently working on.",
                new JsonObject())),
    };

    private McpResponse CallTool(JsonNode id, string sessionId, JsonObject? parameters)
    {
        var name = Text(parameters?["name"]);

        if (name is null || !TheseusMcp.ToolNames.Contains(name))
        {
            return new McpResponse(200, Error(id, -32602, $"Unknown tool: {name}"));
        }

        var arguments = parameters?["arguments"] as JsonObject ?? new JsonObject();

        if (_sessions(sessionId) is not { } session)
        {
            return Ok(id, Result($"Session {sessionId} was not launched by Theseus, so it has no workstream to record time against.", isError: true));
        }

        return Ok(id, name switch
        {
            "start_work" => StartWork(session, arguments),
            "stop_work" => StopWork(session),
            "time_for" => TimeFor(arguments),
            _ => WhoAmI(session),
        });
    }

    private JsonObject StartWork(McpSession session, JsonObject arguments)
    {
        if (Issue(arguments) is not { } issue)
        {
            return Result("issue must be a Jira key, for example NDC-680.", isError: true);
        }

        var now = _clock();

        _work.Append(new WorkMark(session.SessionId, session.WorkstreamKey, WorkMarkKind.Start, issue,
            Text(arguments["summary"])?.Trim(), now));

        return Result($"{session.WorkstreamTitle} is working on {issue} from {Local(now):HH:mm}. Theseus measures the time. "
                      + "Call start_work again for a different issue, or stop_work when you stop.");
    }

    private JsonObject StopWork(McpSession session)
    {
        if (CurrentIssue(session.SessionId) is not { } issue)
        {
            return Result("This session is not working on an issue, so there was nothing to stop.");
        }

        var now = _clock();

        _work.Append(new WorkMark(session.SessionId, session.WorkstreamKey, WorkMarkKind.Stop, null, null, now));

        return Result($"Stopped work on {issue} at {Local(now):HH:mm}.");
    }

    private JsonObject TimeFor(JsonObject arguments)
    {
        if (Issue(arguments) is not { } issue)
        {
            return Result("issue must be a Jira key, for example NDC-680.", isError: true);
        }

        var now = _clock();

        if (!TryWhen(Text(arguments["from"]), StartOfDay(now), out var from)
            || !TryWhen(Text(arguments["to"]), now, out var to))
        {
            return Result("from and to must be ISO 8601 dates or date-times, for example 2026-09-17.", isError: true);
        }

        if (to <= from)
        {
            return Result("to must be later than from.", isError: true);
        }

        var time = IssueTimeCalculator.For(issue, _work.Read(), _attention(), from, to, now);

        var report = new JsonObject
        {
            ["issue"] = issue,
            ["from"] = Local(from).ToString("o", CultureInfo.InvariantCulture),
            ["to"] = Local(to).ToString("o", CultureInfo.InvariantCulture),
            ["attendedMinutes"] = Minutes(time.Attended),
            ["attended"] = Say(time.Attended),
            ["machineMinutes"] = Minutes(time.Machine),
            ["machine"] = Say(time.Machine),
            ["workstreams"] = new JsonArray([.. time.Workstreams.Select(workstream => (JsonNode?)workstream)]),
            ["note"] = "Attended is time the user spent at this work's tab; machine is time sessions spent working on it. "
                       + "They are never added together. Ask the user which to log, and get their approval before writing a worklog.",
        };

        return Result(report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private JsonObject WhoAmI(McpSession session) =>
        Result(new JsonObject
        {
            ["sessionId"] = session.SessionId,
            ["workstream"] = session.WorkstreamKey,
            ["title"] = session.WorkstreamTitle,
            ["workingOn"] = CurrentIssue(session.SessionId),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    private string? CurrentIssue(string sessionId) =>
        _work.Read().LastOrDefault(mark => string.Equals(mark.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)) is
        { Kind: WorkMarkKind.Start, Issue: { } issue }
            ? issue
            : null;

    private bool TryWhen(string? text, DateTimeOffset fallback, out DateTimeOffset when)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            when = fallback;
            return true;
        }

        if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            var midnight = date.ToDateTime(TimeOnly.MinValue);
            when = new DateTimeOffset(midnight, _zone.GetUtcOffset(midnight));
            return true;
        }

        return DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out when);
    }

    private DateTimeOffset StartOfDay(DateTimeOffset now)
    {
        var midnight = Local(now).Date;

        return new DateTimeOffset(midnight, _zone.GetUtcOffset(midnight));
    }

    private DateTimeOffset Local(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, _zone);

    private static string? Issue(JsonObject arguments) =>
        Text(arguments["issue"])?.Trim().ToUpperInvariant() is { } issue && IssueKey().IsMatch(issue) ? issue : null;

    private static int Minutes(TimeSpan span) => (int)Math.Round(span.TotalMinutes);

    private static string Say(TimeSpan span) =>
        $"{(int)span.TotalHours}h{span.Minutes.ToString("00", CultureInfo.InvariantCulture)}";

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static JsonObject Tool(string name, string description, JsonObject properties, params string[] required)
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };

        if (required.Length > 0)
        {
            schema["required"] = new JsonArray([.. required.Select(field => (JsonNode?)field)]);
        }

        return new JsonObject { ["name"] = name, ["description"] = description, ["inputSchema"] = schema };
    }

    private static JsonObject Property(string description) =>
        new() { ["type"] = "string", ["description"] = description };

    private static JsonObject Result(string text, bool isError = false) => new()
    {
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["isError"] = isError,
    };

    private static McpResponse Ok(JsonNode id, JsonObject result) =>
        new(200, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToJsonString());

    private static string Error(JsonNode? id, int code, string message) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString();

    [GeneratedRegex(@"^[A-Z][A-Z0-9]{1,9}-\d{1,6}$")]
    private static partial Regex IssueKey();
}
