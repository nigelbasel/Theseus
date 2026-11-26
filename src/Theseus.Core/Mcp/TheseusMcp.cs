using System.Text.Json.Nodes;

namespace Theseus.Core.Mcp;

/// <summary>Where the session time server listens, and how a launched session is told to reach it.</summary>
public static class TheseusMcp
{
    /// <summary>The port it listens on, on localhost only.</summary>
    public const int DefaultPort = 47813;

    /// <summary>The name sessions see its tools under: <c>mcp__theseus__start_work</c>.</summary>
    public const string ServerName = "theseus";

    /// <summary>Every tool the server offers.</summary>
    public static readonly IReadOnlyList<string> ToolNames =
        [
            "start_work", "stop_work", "time_for", "whoami", "locks", "lock_acquire", "lock_release", "related", "report_back",
            "propose_workstream", "proposal_status", "withdraw_proposal", "link_workstream", "unlink_workstream",
            "approve_proposal", "reject_proposal", "record_work_item", "name_work_items",
            "start_workstream", "stop_workstream", "pause_workstream", "resume_workstream", "workstreams",
            "gates", "accept_spec", "events", "rule_resource",
            "now_at", "record_round", "tick_criterion", "add_objective", "record_decision", "set_waiting",
            AskOwnerTool.Name,
            DutyTools.SetDuty, DutyTools.ListDuties, DutyTools.PauseDuty, DutyTools.EndDuty,
        ];

    /// <summary>
    /// The tools a launched session may use without being asked. Those that change the board are left
    /// out (decision 10, 2026-10-21): a report, a proposal or its withdrawal, a link made or removed, and
    /// a proposal approved or rejected all pass the person in the tab before they are sent. So do a new objective and a new
    /// round in a project file, which are the owner's acts (1.1g, decision G9, 2026-11-22).
    /// </summary>
    public static readonly IReadOnlyList<string> PreApproved =
        [.. ToolNames.Where(tool => tool is not ("report_back" or "propose_workstream" or "withdraw_proposal"
                                       or "link_workstream" or "unlink_workstream" or "approve_proposal" or "reject_proposal"
                                       or "start_workstream" or "stop_workstream" or "pause_workstream" or "resume_workstream"
                                       or "accept_spec" or "add_objective" or "record_round" or "rule_resource"
                                       or DutyTools.SetDuty or DutyTools.PauseDuty or DutyTools.EndDuty))];

    /// <summary>
    /// The <c>--allowedTools</c> value that lets a launched session use exactly these tools without being
    /// asked each time (Nigel, 2026-09-17 for the time tools, 2026-10-06 for the lock tools). Named one by
    /// one rather than with a wildcard, so a tool added to the server later is not approved without anyone
    /// deciding it should be. None of them writes to Jira, and none can reorder, cancel or take another
    /// session's lock; those stay on the board.
    /// </summary>
    public static string AllowedTools => string.Join(",", PreApproved.Select(tool => $"mcp__{ServerName}__{tool}"));

    /// <summary>
    /// The endpoint for one session. The session is identified by its path rather than by anything it
    /// says, so a session cannot record work against a workstream that is not its own.
    /// </summary>
    public static string UrlFor(int port, string sessionId) =>
        $"http://localhost:{port}/mcp/{Uri.EscapeDataString(sessionId)}";

    /// <summary>The <c>--mcp-config</c> value that points one session at the server.</summary>
    public static string ConfigJson(int port, string sessionId) =>
        new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                [ServerName] = new JsonObject
                {
                    ["type"] = "http",
                    ["url"] = UrlFor(port, sessionId),
                },
            },
        }.ToJsonString();
}
