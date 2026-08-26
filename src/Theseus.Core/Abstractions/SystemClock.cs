namespace Theseus.Core.Abstractions;

/// <inheritdoc />
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
