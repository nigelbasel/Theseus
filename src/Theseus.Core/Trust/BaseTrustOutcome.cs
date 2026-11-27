namespace Theseus.Core.Trust;

/// <summary>What keeping the base folders trusted came to.</summary>
/// <param name="Trusted">The keys written, each one that was missing.</param>
/// <param name="Refused">Base folders never trusted: a home folder, a drive's root, a holdout, or one inside a repository.</param>
/// <param name="Failure">Why writing failed, or null.</param>
public sealed record BaseTrustOutcome(IReadOnlyList<string> Trusted, IReadOnlyList<string> Refused, string? Failure);
