namespace Theseus.Core.Projects;

/// <summary>One of a workstream's owner's gates, as <c>gates()</c> gives it (1.1e AC 28).</summary>
/// <param name="Gate">"spec", or "rule" for a bug.</param>
/// <param name="Version">The version on disk now, short; null when none is written.</param>
/// <param name="Status">Where it stands for that version.</param>
public sealed record OwnerGate(string Gate, string? Version, OwnerGateStatus Status)
{
    /// <summary>The latest acceptance, of this version or, when it changed since, an earlier one.</summary>
    public SpecAcceptance? Acceptance { get; init; }

    /// <summary>What the session reported the owner said, waiting for him to confirm.</summary>
    public ReportedAcceptance? Report { get; init; }

    /// <summary>Whether the session may go on past it: accepted at the version on disk now.</summary>
    public bool IsPassed => Status == OwnerGateStatus.Accepted;
}
