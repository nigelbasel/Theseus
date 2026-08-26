namespace Theseus.Core.Abstractions;

/// <summary>The current time, as a seam so elapsed-time decisions are testable.</summary>
public interface ISystemClock
{
    /// <summary>The current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }
}
