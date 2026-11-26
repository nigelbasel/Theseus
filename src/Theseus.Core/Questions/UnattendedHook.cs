using System.Text.Json;
using Theseus.Core.Persistence;
using Theseus.Core.Stopping;

namespace Theseus.Core.Questions;

/// <summary>
/// What the hook refuses a card of an unattended project (2.4 W2; Nigel, 2026-11-26, "run a scenario end to end without
/// him"): nobody is at the desk, so the question tool would hold the session until morning, and a turn that ends on a
/// question asked in prose looks finished on the board. Both are turned into parking, the method's decide, record or park
/// ladder. Registered only in an unattended card's own settings, with <see cref="Argument"/>, so the hook knows from its
/// command line alone and works while Theseus is down.
/// </summary>
public static class UnattendedHook
{
    /// <summary>The argument the hook is given in an unattended card's settings.</summary>
    public const string Argument = "--unattended";

    /// <summary>The question tool, which an unattended card is refused.</summary>
    public const string QuestionTool = "AskUserQuestion";

    /// <summary>The tools that excuse a turn ending on its question: it was parked, or it is the report's summary.</summary>
    public static readonly IReadOnlyList<string> Excusing =
        [$"mcp__{Mcp.TheseusMcp.ServerName}__{Mcp.AskOwnerTool.Name}", $"mcp__{Mcp.TheseusMcp.ServerName}__report_back"];

    /// <summary>Why the question tool is refused.</summary>
    public const string ToolRefusal =
        "This project runs unattended, so the question tool is refused: nobody is at the desk to answer it, and it would "
        + "hold this session until they are. Decide and record it if the method's ladder lets you (record_decision, by "
        + "\"<card> (default, for review)\"); otherwise park it: call ask_owner with the question, its options and your "
        + "recommendation, record who it waits on with set_waiting, and carry on with the work that does not depend on it.";

    /// <summary>Why a turn that ends on a prose question is refused, once.</summary>
    public const string StopText =
        "This project runs unattended: nobody is at the desk to answer a question asked in prose, and a turn that ends on "
        + "one looks finished on the board. Decide and record it if the method's ladder lets you; otherwise park it: call "
        + "ask_owner with the question, its options and your recommendation, record who it waits on with set_waiting, and "
        + "carry on with whatever does not depend on the answer. When nothing is left, commit, write your handoff and call "
        + "report_back with verdict partial. If you were not asking anything, end your turn again.";

    /// <summary>The PreToolUse reply refusing the question tool, or null for any other tool.</summary>
    public static string? PreToolUseReply(string? toolName) =>
        toolName != QuestionTool
            ? null
            : JsonSerializer.Serialize(new
            {
                hookSpecificOutput = new
                {
                    hookEventName = "PreToolUse",
                    permissionDecision = "deny",
                    permissionDecisionReason = ToolRefusal,
                },
            }, TheseusJson.Compact);

    /// <summary>
    /// On Stop, the refusal of a turn whose last words ask the owner something, or null. Once only: a turn a stop hook
    /// already extended is let end. A subagent's turn, and a turn that called ask_owner or report_back, are let end too.
    /// </summary>
    /// <param name="input">What the hook was given.</param>
    /// <param name="readTail">The end of a transcript as text (<see cref="TranscriptTail.Read"/>), or null.</param>
    public static string? StopRefusal(StopHookInput input, Func<string?, string?> readTail)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(readTail);

        if (input.HookEventName != "Stop" || input.StopHookActive || input.AgentId is not null)
        {
            return null;
        }

        var tail = readTail(input.TranscriptPath);

        if (ProseQuestion.AskingLine(input.LastAssistantMessage ?? TranscriptTail.LastAssistantTextIn(tail)) is null)
        {
            return null;
        }

        return TranscriptTail.ToolsCalledThisTurn(tail).Any(Excusing.Contains) ? null : StopText;
    }
}
