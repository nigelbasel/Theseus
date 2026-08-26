using Theseus.Core.Sessions;

namespace Theseus.Core.Abstractions;

/// <summary>Reads the workflow a session is running right now.</summary>
public interface IWorkflowActivityReader
{
    /// <summary>The session's most recent workflow run, or null when it has run none.</summary>
    WorkflowActivity? Read(string? sessionId);
}
