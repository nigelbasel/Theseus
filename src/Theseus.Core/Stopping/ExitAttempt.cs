namespace Theseus.Core.Stopping;

/// <summary>One attempt to type /exit into a stopped session.</summary>
/// <param name="Result">What it did.</param>
/// <param name="AtUtc">When it was made.</param>
public sealed record ExitAttempt(ExitResult Result, DateTimeOffset AtUtc);
