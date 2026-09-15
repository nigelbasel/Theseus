namespace Theseus.Core.Launching;

/// <summary>How a workstream that has run before is brought back.</summary>
public enum StartMode
{
    /// <summary>Reopen its last conversation, with everything that conversation held still in context.</summary>
    Resume,

    /// <summary>Open a new conversation, told to read the handoff instead.</summary>
    Fresh,
}
