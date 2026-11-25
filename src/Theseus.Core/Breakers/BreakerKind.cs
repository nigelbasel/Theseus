namespace Theseus.Core.Breakers;

/// <summary>Which circuit breaker tripped.</summary>
public enum BreakerKind
{
    /// <summary>Runs in a row crashed or failed to start.</summary>
    Crashes,

    /// <summary>Runs in a row made no progress.</summary>
    NoProgress,

    /// <summary>Runs in a row failed with the same error.</summary>
    SameError,

    /// <summary>A run went past its time limit and its grace.</summary>
    OverTime,
}
