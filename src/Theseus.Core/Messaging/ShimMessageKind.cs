namespace Theseus.Core.Messaging;

/// <summary>The message types exchanged between the launcher shim and the application.</summary>
public enum ShimMessageKind
{
    /// <summary>Shim to app: presents a ticket and the shim's own process id.</summary>
    Hello,

    /// <summary>App to shim: the launch specification the ticket redeems.</summary>
    LaunchSpec,

    /// <summary>Shim to app: Claude Code started, with its process id.</summary>
    Started,

    /// <summary>Shim to app: a failed resume was retried under a freshly minted identifier.</summary>
    Reminted,

    /// <summary>Shim to app: Claude Code exited, with its exit code and lifetime.</summary>
    Exited,

    /// <summary>Either direction: the request could not be honoured.</summary>
    Error,
}
