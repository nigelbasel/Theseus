namespace Theseus.Core.Mcp;

/// <summary>Which of the tools a project's session acts on a descendant with (1.1e AC 14).</summary>
public enum WorkstreamCall
{
    /// <summary>start_workstream.</summary>
    Start,

    /// <summary>stop_workstream.</summary>
    Stop,

    /// <summary>pause_workstream.</summary>
    Pause,

    /// <summary>resume_workstream.</summary>
    Resume,
}
