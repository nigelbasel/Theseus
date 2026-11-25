using System.Text.Json;

namespace Theseus.Core.Running;

/// <summary>
/// Reads the stream-json lines of a headless run (shapes measured with Claude Code 2.1.331, headless-runner
/// measurements, 2026-11-24). Anything unknown or malformed is ignored: the raw line is kept in the run's log anyway.
/// </summary>
public static class RunStreamParser
{
    private const int LineLength = 160;

    /// <summary>The event a line carries, or null for one Theseus does not act on.</summary>
    public static RunStreamEvent? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            return Text(root, "type") switch
            {
                "system" when Text(root, "subtype") == "init" && Text(root, "session_id") is { } session => new RunInitEvent(session),
                "assistant" when root.TryGetProperty("message", out var message) => Assistant(message),
                "result" => Result(root),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The stdin line that gives a headless run a user message: its prompt, or a stop request.</summary>
    public static string UserMessage(string text) =>
        JsonSerializer.Serialize(new { type = "user", message = new { role = "user", content = text } });

    /// <summary>The stdin line that interrupts a run's turn, as Esc does in a tab.</summary>
    public static string Interrupt(string requestId) =>
        JsonSerializer.Serialize(new { type = "control_request", request_id = requestId, request = new { subtype = "interrupt" } });

    private static RunAssistantEvent Assistant(JsonElement message)
    {
        string? line = null;

        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                line = Text(block, "type") switch
                {
                    "text" => Text(block, "text"),
                    "tool_use" => Tool(block),
                    _ => null,
                };

                if (!string.IsNullOrWhiteSpace(line))
                {
                    break;
                }
            }
        }

        return new RunAssistantEvent(Text(message, "id"), Shorten(line), Usage(message));
    }

    private static RunResultEvent Result(JsonElement root)
    {
        decimal? cost = root.TryGetProperty("total_cost_usd", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : null;

        var isError = root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
        var text = Text(root, "result");

        if (text is null && root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            text = errors.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString()).FirstOrDefault();
        }

        return new RunResultEvent(cost, Usage(root), Text(root, "subtype") ?? string.Empty, isError, text);
    }

    private static string Tool(JsonElement block)
    {
        var name = Text(block, "name") ?? "tool";

        if (!block.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
        {
            return name;
        }

        var detail = Text(input, "command") ?? Text(input, "file_path") ?? Text(input, "pattern") ?? Text(input, "description");

        return detail is null ? name : $"{name}: {detail}";
    }

    private static RunTokens Usage(JsonElement owner)
    {
        if (!owner.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return RunTokens.None;
        }

        return new RunTokens(
            Number(usage, "input_tokens"),
            Number(usage, "output_tokens"),
            Number(usage, "cache_creation_input_tokens"),
            Number(usage, "cache_read_input_tokens"));
    }

    private static long Number(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : 0;

    private static string? Text(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Shorten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var first = text.Trim().Split('\n')[0].Trim();

        return first.Length <= LineLength ? first : first[..(LineLength - 1)] + "…";
    }
}
