using System.Text.Json;

namespace Theseus.Tests.Running;

/// <summary>Stream-json lines in the shapes Claude Code 2.1.331 wrote when measured on 2026-11-24, cut to what is read.</summary>
internal static class StreamLines
{
    public const string Init =
        """{"type":"system","subtype":"init","cwd":"C:\\work","session_id":"a3263659-ae89-41c5-a7ee-71702c797ee8","permissionMode":"auto"}""";

    public const string Hook = """{"type":"system","subtype":"hook_started","hook_name":"SessionStart"}""";

    public const string BudgetResult =
        """{"type":"result","subtype":"error_max_budget_usd","is_error":true,"total_cost_usd":0.2059158,"usage":{"input_tokens":2,"output_tokens":300,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}""";

    public static string Text(string id, string text, long output = 4, long input = 2, long creation = 0, long read = 0) =>
        Assistant(id, new { type = "text", text }, Usage(input, output, creation, read));

    public static string Tool(string id, string command, long output = 10) =>
        Assistant(id, new { type = "tool_use", name = "Bash", input = new { command } }, Usage(1, output, 0, 0));

    public static string Result(decimal cost, string subtype = "success", bool error = false, string result = "ok", long output = 4) =>
        JsonSerializer.Serialize(new
        {
            type = "result",
            subtype,
            is_error = error,
            result,
            total_cost_usd = cost,
            usage = Usage(2, output, 50959, 17421),
        });

    private static string Assistant(string id, object block, object usage) =>
        JsonSerializer.Serialize(new { type = "assistant", message = new { id, content = new[] { block }, usage } });

    private static object Usage(long input, long output, long creation, long read) => new
    {
        input_tokens = input,
        output_tokens = output,
        cache_creation_input_tokens = creation,
        cache_read_input_tokens = read,
    };
}
