using Theseus.Core.Abstractions;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Windows;

/// <summary>A workflow reader that finds no runs, for a drawer built in a window test.</summary>
internal sealed class NoWorkflowRuns : IWorkflowRunReader
{
    public IReadOnlyList<WorkflowRun> Read(string? sessionId) => [];
}
