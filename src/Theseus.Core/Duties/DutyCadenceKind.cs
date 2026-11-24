namespace Theseus.Core.Duties;

/// <summary>How a duty's runs are spaced.</summary>
public enum DutyCadenceKind
{
    /// <summary>Every fixed interval, counted from when the duty was set.</summary>
    Every,

    /// <summary>At set local times of day, optionally on chosen weekdays only.</summary>
    At,
}
