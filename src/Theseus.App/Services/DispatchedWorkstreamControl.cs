using System.Windows.Threading;
using Theseus.Core.Mcp;

namespace Theseus.App.Services;

/// <summary>
/// The board's workstream control, called from the MCP server's threads (1.1e AC 14): each call runs on the UI thread, as
/// the owner's own buttons do, and the session waits for its answer, for at most half a minute: a board that is busy, or
/// held by a dialog, answers that it is busy rather than holding the session (review of PR 3, finding 14).
/// </summary>
public sealed class DispatchedWorkstreamControl : IWorkstreamControl
{
    private readonly IWorkstreamControl _inner;
    private readonly Dispatcher _dispatcher;

    /// <summary>Creates the control over the board's own.</summary>
    public DispatchedWorkstreamControl(IWorkstreamControl inner, Dispatcher dispatcher)
    {
        _inner = inner;
        _dispatcher = dispatcher;
    }

    /// <summary>How long a session waits for the board.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public string? Start(string key, string? instruction, string caller) => OnBoard(() => _inner.Start(key, instruction, caller));

    /// <inheritdoc />
    public string? Stop(string key, string caller) => OnBoard(() => _inner.Stop(key, caller));

    /// <inheritdoc />
    public string? Pause(string key, string caller, string? reason) => OnBoard(() => _inner.Pause(key, caller, reason));

    /// <inheritdoc />
    public string? Resume(string key, string? instruction, string caller) => OnBoard(() => _inner.Resume(key, instruction, caller));

    private string? OnBoard(Func<string?> call)
    {
        var operation = _dispatcher.InvokeAsync(call);

        if (operation.Wait(Wait) == DispatcherOperationStatus.Completed)
        {
            return operation.Result;
        }

        operation.Abort();

        return "The board did not answer in time (a dialog may be open): call workstreams to see whether it went ahead before asking again.";
    }
}
