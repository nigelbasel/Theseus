using Theseus.Core.Abstractions;

namespace Theseus.Tests.Duties;

/// <summary>A clock the duty tests move by hand.</summary>
public sealed class DutyTestClock(DateTimeOffset start) : ISystemClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = start;
}
