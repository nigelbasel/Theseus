namespace Theseus.Core.Running;

/// <summary>Who or what asked for a run: shown on the card and recorded with the run.</summary>
public enum RunReason
{
    /// <summary>The owner pressed Start.</summary>
    Owner,

    /// <summary>A recurring duty came due (Nigel, 2026-11-24).</summary>
    Duty,

    /// <summary>The overnight queue resumed an idle card whose piece of work is not done (Nigel, 2026-11-24).</summary>
    Queue,

    /// <summary>Theseus started a verifier card for a build (Nigel, 2026-11-23).</summary>
    Verifier,
}
