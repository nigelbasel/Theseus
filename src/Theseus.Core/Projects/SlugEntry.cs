namespace Theseus.Core.Projects;

/// <summary>One slug of an objective as the project view lays it out (spec AC 4 and 10).</summary>
/// <param name="Row">Its row in the project file.</param>
/// <param name="Stage">Where its work stands.</param>
/// <param name="CardKeys">The cards working under it: one, or none. More than one only if the board file was edited by hand.</param>
/// <param name="VerifierKeys">The verifier cards checking it that have not ended; each sits in verify beside it.</param>
/// <param name="IsEarlierSlug">Whether another slug of the same workstream (a row with the same Workstream) has its card.</param>
public sealed record SlugEntry(
    ProjectWorkstreamRow Row,
    WorkstreamStage Stage,
    IReadOnlyList<string> CardKeys,
    IReadOnlyList<string> VerifierKeys,
    bool IsEarlierSlug)
{
    /// <summary>
    /// No card works under it and nothing of it is written: "not started". A slug with no card whose work exists, such
    /// as an earlier slug, shows at its own stage instead, not as a card.
    /// </summary>
    public bool IsNotStarted => CardKeys.Count == 0 && !Stage.HasStarted;

    /// <summary>Where a verifier card sits, whatever the artefacts say (AC 10).</summary>
    public static PipelineStage VerifierStage => PipelineStage.Verify;
}
