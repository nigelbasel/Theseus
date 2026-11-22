using System.Globalization;

namespace Theseus.Core.Projects;

/// <summary>
/// What a session may ask of its owner's gate and report about it (1.1e AC 28): <c>gates()</c> reads the gate of the
/// workstream the calling card is in; <c>accept_spec</c> keeps the owner's words for him to confirm on the board, and is
/// refused when the version named is not the spec's current text. Nothing here accepts anything.
/// </summary>
public static class SpecGates
{
    /// <summary>The spec (a bug's rule) of a project's workstream as read now, or null when it has none written.</summary>
    public static SpecAtGate? SpecOf(ProjectFileReading reading, string slug)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (reading.File?.ObjectiveOf(slug) is not { } objective
            || reading.Artefacts.GetValueOrDefault(slug) is not { } artefacts)
        {
            return null;
        }

        return SpecAtGate.For(reading.Name, slug, StageCalculator.PipelineFor(objective.Pipelines, artefacts.Files), artefacts);
    }

    /// <summary>The gate's state for the version on disk now.</summary>
    /// <param name="spec">The spec as read now, or null when none is written yet.</param>
    /// <param name="accepted">The latest acceptance of the workstream's spec, any version.</param>
    /// <param name="reported">What its session reported the owner said, waiting for him to confirm.</param>
    public static OwnerGate State(SpecAtGate? spec, SpecAcceptance? accepted, ReportedAcceptance? reported)
    {
        if (spec is null)
        {
            return new OwnerGate("spec", null, OwnerGateStatus.NotWritten);
        }

        var gate = spec.IsRule ? "rule" : "spec";

        if (accepted is not null && string.Equals(accepted.ContentHash, spec.ContentHash, StringComparison.Ordinal))
        {
            return new OwnerGate(gate, spec.ShortVersion, OwnerGateStatus.Accepted) { Acceptance = accepted };
        }

        if (reported is not null && string.Equals(reported.ContentHash, spec.ContentHash, StringComparison.Ordinal))
        {
            return new OwnerGate(gate, spec.ShortVersion, OwnerGateStatus.ReportedWaitingForConfirmation) { Report = reported };
        }

        return new OwnerGate(gate, spec.ShortVersion, accepted is null ? OwnerGateStatus.WaitingForOwner : OwnerGateStatus.ChangedSinceAccepted)
        {
            Acceptance = accepted,
        };
    }

    /// <summary>
    /// Why a card cannot report its owner's acceptance, or null (1.1e AC 28; review of PR 6, findings 1 and 6): it must be
    /// in a project, its own workstream, with no role of its own (a verifier, scenario writer or updater has no owner's
    /// gate), and not already accepted at the version on disk.
    /// </summary>
    public static string? WhoMayReport(string? project, string? ownSlug, Domain.WorkstreamRole role, string slug, bool acceptedAlready,
        string? version)
    {
        if (role != Domain.WorkstreamRole.None)
        {
            return NoGateForRole;
        }

        if (project is null || ownSlug is null)
        {
            return "This workstream is not in a project, so it has no spec gate to report on.";
        }

        if (!string.Equals(ownSlug, slug, StringComparison.OrdinalIgnoreCase))
        {
            return $"You can report only for your own workstream, {ownSlug}.";
        }

        return acceptedAlready ? $"Version {version} is accepted already: call gates()." : null;
    }

    /// <summary>What a verifier, scenario writer or updater is told of gates: it has none.</summary>
    public const string NoGateForRole =
        "This card checks, writes scenarios for or updates a project; it has no owner's gate of its own. Do not write a spec.";

    /// <summary>What <c>gates()</c> says of a workstream's gate: where it stands, what the record holds, and what to do.</summary>
    public static string Describe(OwnerGate gate, string project, string slug, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(zone);
        var what = $"The {gate.Gate} of {slug} in {project}";

        return gate.Status switch
        {
            OwnerGateStatus.NotWritten => $"{slug} in {project} has no spec written yet. Write it, then stop and ask the owner to accept it.",
            OwnerGateStatus.Accepted => $"{what}, version {gate.Version}: accepted ({gate.Acceptance!.Describe(zone)}). You may plan.",
            OwnerGateStatus.ReportedWaitingForConfirmation =>
                $"{what}, version {gate.Version}: you reported the owner accepted it in your tab (\"{gate.Report!.OwnerWords}\"); it waits for him to confirm on the board. Do not plan yet.",
            OwnerGateStatus.ChangedSinceAccepted =>
                $"{what}, version {gate.Version}: changed since he accepted {gate.Acceptance!.Describe(zone)}. It waits for him again. Do not plan yet.",
            _ => $"{what}, version {gate.Version}: waiting for the owner. Do not plan yet: he accepts it on the board, or tells you in this tab and you report it with accept_spec.",
        };
    }

    /// <summary>
    /// The report of the owner's words, or why it is refused: no spec, or a version that is not the spec's current text
    /// (the full version or at least its first seven characters).
    /// </summary>
    public static (ReportedAcceptance? Report, string? Refusal) Report(SpecAtGate? spec, string version, string cardKey, string cardTitle,
        string ownerWords, DateTimeOffset now)
    {
        if (spec is null)
        {
            return (null, "This workstream has no spec written, so there is nothing to accept.");
        }

        var named = (version ?? string.Empty).Trim();

        if (named.Length < 7 || !spec.ContentHash.StartsWith(named, StringComparison.OrdinalIgnoreCase))
        {
            return (null, string.Create(CultureInfo.InvariantCulture,
                $"Version {named} is not the {(spec.IsRule ? "rule" : "spec")}'s current text, which is version {spec.ShortVersion}. Show the owner the current text and ask again."));
        }

        if (string.IsNullOrWhiteSpace(ownerWords))
        {
            return (null, "Quote the owner's words, as he said them in this tab.");
        }

        return (new ReportedAcceptance(spec.Project, spec.Slug, spec.ContentHash, cardKey, cardTitle, ownerWords.Trim(), now), null);
    }
}
