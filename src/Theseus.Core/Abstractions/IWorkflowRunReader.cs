using Theseus.Core.Sessions;

namespace Theseus.Core.Abstractions;

/// <summary>Reads the workflows a session has run.</summary>
public interface IWorkflowRunReader
{
    /// <summary>One session's finished workflow runs, newest first.</summary>
    IReadOnlyList<WorkflowRun> Read(string? sessionId);
}
