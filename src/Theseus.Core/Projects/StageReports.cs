namespace Theseus.Core.Projects;

/// <summary>
/// What sessions have told Theseus about their stages (1.1f E24, E30a): what each card's session is doing now, and the
/// stages each piece's session said it skipped. Kept by Theseus, read by <see cref="PiecesOfWork"/>.
/// </summary>
/// <param name="NowAtOf">The live report of a card's session, by card key, or null when it has said nothing.</param>
/// <param name="SkipsOf">The stages a piece's session said it skipped, by project and piece.</param>
public sealed record StageReports(
    Func<string, SessionStageReport?> NowAtOf,
    Func<string, string, IReadOnlyDictionary<PipelineStage, string>> SkipsOf)
{
    /// <summary>No session has said anything.</summary>
    public static StageReports None { get; } = new(_ => null, (_, _) => new Dictionary<PipelineStage, string>());
}
