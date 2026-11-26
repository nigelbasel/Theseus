using System.Text.Json.Nodes;
using Theseus.Core.Questions;

namespace Theseus.Core.Mcp;

/// <summary>
/// <c>ask_owner</c> (1.6): a session records the question it waits on, so it shows on its card, in Waits for you and in
/// Needs you, where the owner sees it without going to the tab (Nigel, 2026-11-22). It shows until he next types in that
/// tab, or the session says it has its answer.
/// </summary>
public sealed class AskOwnerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "ask_owner";

    /// <summary>The most options a question offers, as the question tool allows.</summary>
    public const int MaxOptions = 4;

    private readonly OwnerQuestionStore _store;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action _changed;

    /// <summary>Creates the tool over the store the hook and the board share.</summary>
    /// <param name="store">Where questions are kept.</param>
    /// <param name="clock">The moment a question is asked.</param>
    /// <param name="changed">Tells the board to look again, so a question shows at once; nothing when null.</param>
    public AskOwnerTool(OwnerQuestionStore store, Func<DateTimeOffset> clock, Action? changed = null)
    {
        _store = store;
        _clock = clock;
        _changed = changed ?? (() => { });
    }

    /// <summary>The tool's definition for tools/list.</summary>
    public static JsonObject Definition { get; } = new()
    {
        ["name"] = Name,
        ["description"] = "Show the owner, in Theseus, a question you need him to answer before you can go on: on your card, "
                          + "in Waits for you and in Needs you. Ask the same question in this tab with the question tool too, "
                          + "so he can answer where he is. Call with answered: true once you have the answer from the question tool.",
        ["inputSchema"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["question"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = $"The question, one line, at most {ProseQuestion.MaxLength} characters.",
                },
                ["options"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = $"Optional: up to {MaxOptions} short answers to choose from.",
                },
                ["why"] = new JsonObject { ["type"] = "string", ["description"] = "Optional: why you need it, one line." },
                ["answered"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "true to clear your open question once answered; no question is needed then.",
                },
            },
        },
    };

    /// <summary>
    /// What a card of an unattended project is told once its question is shown (2.4 W2): it is parked, since nobody will
    /// answer in the tab and the question tool is refused it.
    /// </summary>
    public const string Parked =
        "Parked: shown on the board, on your card, in Waits for you and in Needs you. Nobody is at the desk, so do not ask "
        + "it in this tab. Record who it waits on with set_waiting, and carry on with every part of the work that does not "
        + "depend on the answer; when nothing is left, commit, write your handoff and call report_back with verdict partial.";

    /// <summary>Runs the tool for a session: the reply's text, and whether it was refused.</summary>
    /// <param name="session">The calling session.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="unattended">Whether the session's project is unattended.</param>
    public (string Text, bool IsError) Call(McpSession session, JsonObject arguments, bool unattended = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments["answered"] is JsonValue answered && answered.TryGetValue<bool>(out var isAnswered) && isAnswered)
        {
            _store.Clear(session.SessionId);
            _changed();

            return ("Cleared: your question no longer shows on the board.", false);
        }

        var question = OneLine(arguments["question"]);

        if (question is null)
        {
            return ("ask_owner needs the question, or answered: true.", true);
        }

        if (question.Length > ProseQuestion.MaxLength)
        {
            return ($"The question is {question.Length} characters; keep it to {ProseQuestion.MaxLength} and put the detail in why or in the tab.", true);
        }

        var options = arguments["options"] is JsonArray array
            ? array.Select(OneLine).OfType<string>().ToList()
            : [];

        if (options.Count > MaxOptions)
        {
            return ($"At most {MaxOptions} options.", true);
        }

        if (!_store.Ask(new OwnerQuestion(session.SessionId, question, IsGuess: false, _clock())
            {
                Options = options,
                Why = OneLine(arguments["why"]),
            }))
        {
            return (unattended
                ? "The question could not be recorded, so the owner cannot see it in Theseus. Write it in your handoff and report_back with verdict partial."
                : "The question could not be recorded, so the owner cannot see it in Theseus. Ask it here with the question tool.", true);
        }

        _changed();

        if (unattended)
        {
            return (Parked, false);
        }

        return ("Shown on the board: on your card, in Waits for you and in Needs you, until the owner next types in this tab. "
                + "Ask the same question here with the question tool now, so he can answer where he is; "
                + "once it answers you, call ask_owner with answered: true.", false);
    }

    private static string? OneLine(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return null;
        }

        var line = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return line.Length == 0 ? null : line;
    }
}
