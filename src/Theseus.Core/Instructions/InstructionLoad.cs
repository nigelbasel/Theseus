namespace Theseus.Core.Instructions;

/// <summary>When an instruction file reaches a session's context.</summary>
public enum InstructionLoad
{
    /// <summary>Read before the session does anything.</summary>
    AtStart,

    /// <summary>Read only once the session works with the files it applies to.</summary>
    OnDemand,

    /// <summary>On disk and in the session's reach, but never read.</summary>
    NotLoaded,
}
