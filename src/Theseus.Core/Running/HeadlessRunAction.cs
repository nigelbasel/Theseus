namespace Theseus.Core.Running;

/// <summary>What the owner of a headless run's process must do after a tick.</summary>
public enum HeadlessRunAction
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>
    /// End the process: no session began within the start-up allowance, so nothing can be mid-write. The only case
    /// Theseus ends a run's process itself.
    /// </summary>
    EndProcess,
}
