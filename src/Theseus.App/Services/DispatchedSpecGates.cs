using System.Windows.Threading;
using Theseus.Core.Mcp;

namespace Theseus.App.Services;

/// <summary>
/// gates() and accept_spec from the MCP server's threads (1.1e AC 28): each runs on the UI thread, as the board's own
/// reads do, and the session waits for at most half a minute, as for the workstream tools.
/// </summary>
public sealed class DispatchedSpecGates : ISpecGates
{
    private readonly ISpecGates _inner;
    private readonly Dispatcher _dispatcher;

    /// <summary>Creates them over the board's own.</summary>
    public DispatchedSpecGates(ISpecGates inner, Dispatcher dispatcher)
    {
        _inner = inner;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public string Gates(string callerKey) =>
        OnBoard(() => _inner.Gates(callerKey)) ?? "The board did not answer in time (a dialog may be open): ask again shortly.";

    /// <inheritdoc />
    public string? ReportAcceptance(string callerKey, string slug, string version, string ownerWords)
    {
        var operation = _dispatcher.InvokeAsync(() => _inner.ReportAcceptance(callerKey, slug, version, ownerWords));

        if (operation.Wait(DispatchedWorkstreamControl.Wait) == DispatcherOperationStatus.Completed)
        {
            return operation.Result;
        }

        operation.Abort();

        return "The board did not answer in time (a dialog may be open): call gates() to see whether the report was kept before sending it again.";
    }

    private string? OnBoard(Func<string> call)
    {
        var operation = _dispatcher.InvokeAsync(call);

        if (operation.Wait(DispatchedWorkstreamControl.Wait) == DispatcherOperationStatus.Completed)
        {
            return operation.Result;
        }

        operation.Abort();

        return null;
    }
}
