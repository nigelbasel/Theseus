using System.Globalization;

namespace Theseus.Core.Projects;

/// <summary>
/// A slug's stages and gates from its artefacts (spec AC 6 to 9). A stage is reached when its artefact exists. Inside a
/// project the owner's gate is the spec (a bug's rule), passed only by an acceptance recorded on the board for the
/// spec as it now reads; verify passes only on a <c>pass</c> result block; every other stage passes once a later stage
/// is reached. A card never sits past an owner's gate that has not passed.
/// </summary>
public static class StageCalculator
{
    private static readonly PipelineStage[] FeatureStages =
    [
        PipelineStage.Research, PipelineStage.Spec, PipelineStage.Plan, PipelineStage.Implement,
        PipelineStage.Verify, PipelineStage.Review, PipelineStage.Compound,
    ];

    private static readonly PipelineStage[] BugStages =
    [
        PipelineStage.Research, PipelineStage.Spec, PipelineStage.Implement,
        PipelineStage.Verify, PipelineStage.Review, PipelineStage.Compound,
    ];

    /// <summary>
    /// A pipeline's stages in order: a bug has no plan, and freeform work has none at all (project-view-redesign spec,
    /// code fact 1, 2026-11-20).
    /// </summary>
    public static IReadOnlyList<PipelineStage> StagesOf(PipelineKind pipeline) => pipeline switch
    {
        PipelineKind.Feature => FeatureStages.AsReadOnly(),
        PipelineKind.Bug => BugStages.AsReadOnly(),
        _ => [],
    };

    /// <summary>
    /// The file in <c>work\&lt;slug&gt;\</c> that records a stage: a bug's rule and fix are both sections of fix.md
    /// (<see cref="BugArtefacts"/>), so for them the file existing is not the stage being reached.
    /// </summary>
    public static string RecordFileOf(PipelineKind pipeline, PipelineStage stage) => (pipeline, stage) switch
    {
        (PipelineKind.Bug, PipelineStage.Research) => "reproduction.md",
        (PipelineKind.Bug, PipelineStage.Spec or PipelineStage.Implement) => "fix.md",
        (_, PipelineStage.Research) => "research.md",
        (_, PipelineStage.Spec) => "spec.md",
        (_, PipelineStage.Plan) => "plan.md",
        (_, PipelineStage.Implement) => "implementation.md",
        (_, PipelineStage.Verify) => "evidence.md",
        (_, PipelineStage.Review) => "review.md",
        _ => "lessons.md",
    };

    /// <summary>
    /// The pipeline a slug runs: its objective's, or, where the objective names several, bug when the slug has a
    /// reproduction.md or fix.md and the other one named otherwise. Null when none is chosen yet. Where that is none or
    /// freeform, a spec.md or fix.md written gives it the pipeline whose gate that record is: a spec written waits for the
    /// owner whatever the objective says, and the board, gates() and now_at all read it so (Nigel, 2026-11-26: "I dont see
    /// any acccept spec notifications in the ui").
    /// </summary>
    public static PipelineKind? PipelineFor(IReadOnlyList<PipelineKind> pipelines, IReadOnlySet<string> files)
    {
        ArgumentNullException.ThrowIfNull(pipelines);
        ArgumentNullException.ThrowIfNull(files);

        var named = Named(pipelines, files);

        if (named is not (null or PipelineKind.Freeform))
        {
            return named;
        }

        return files.Contains("fix.md") ? PipelineKind.Bug
            : files.Contains("spec.md") ? PipelineKind.Feature
            : named;
    }

    private static PipelineKind? Named(IReadOnlyList<PipelineKind> pipelines, IReadOnlySet<string> files)
    {
        if (pipelines.Count <= 1)
        {
            return pipelines.Count == 1 ? pipelines[0] : null;
        }

        var looksLikeABug = files.Contains("reproduction.md") || files.Contains("fix.md");

        return looksLikeABug && pipelines.Contains(PipelineKind.Bug)
            ? PipelineKind.Bug
            : pipelines.First(pipeline => pipeline != PipelineKind.Bug);
    }

    /// <summary>The noun the card names when work has run past a gate, as in "ahead of your gate: plan written".</summary>
    public static string ArtefactName(PipelineKind pipeline, PipelineStage stage) => (pipeline, stage) switch
    {
        (PipelineKind.Bug, PipelineStage.Research) => "reproduction",
        (PipelineKind.Bug, PipelineStage.Spec) => "rule",
        (PipelineKind.Bug, PipelineStage.Implement) => "fix",
        (_, PipelineStage.Implement) => "implementation",
        (_, PipelineStage.Verify) => "evidence",
        (_, PipelineStage.Compound) => "lessons",
        _ => stage.ToString().ToLowerInvariant(),
    };

    /// <summary>Works out where a slug's work stands.</summary>
    /// <param name="pipelines">The pipelines its objective names.</param>
    /// <param name="artefacts">What its folder in the project home holds.</param>
    /// <param name="acceptance">The owner's latest acceptance of its spec, from Theseus's store, or null.</param>
    public static WorkstreamStage Calculate(IReadOnlyList<PipelineKind> pipelines, WorkstreamArtefacts artefacts, SpecAcceptance? acceptance)
    {
        ArgumentNullException.ThrowIfNull(artefacts);

        var pipeline = PipelineFor(pipelines, artefacts.Files);

        if (pipeline is null or PipelineKind.Freeform)
        {
            return new WorkstreamStage(pipeline, [], [], null, null, artefacts.Source);
        }

        var stages = pipeline == PipelineKind.Bug ? BugStages : FeatureStages;
        var reached = stages.Where(stage => IsReached(pipeline.Value, stage, artefacts)).ToList();
        var evidence = EvidenceBlockReader.Last(artefacts.EvidenceText);
        var claims = evidence?.Gates ?? [];

        if (reached.Count == 0)
        {
            return new WorkstreamStage(pipeline, [], [], null, null, artefacts.Source) { EvidenceClaims = claims };
        }

        var furthest = reached[^1];

        if (artefacts.IsBeforeKit)
        {
            return new WorkstreamStage(pipeline, reached, [], furthest, "before method-v0.2", artefacts.Source)
            {
                EvidenceClaims = claims,
            };
        }

        var gates = stages.Select(stage => Gate(pipeline.Value, stage, reached, artefacts, evidence, acceptance)).ToList();
        var spec = gates.First(gate => gate.Stage == PipelineStage.Spec);
        var position = furthest;
        string? note = null;

        if (spec.State is not (GateState.Passed or GateState.Skipped) && furthest > PipelineStage.Spec)
        {
            position = PipelineStage.Spec;
            note = $"ahead of your gate: {ArtefactName(pipeline.Value, furthest)} written";
        }

        return new WorkstreamStage(pipeline, reached, gates, position, note, artefacts.Source) { EvidenceClaims = claims };
    }

    private static bool IsReached(PipelineKind pipeline, PipelineStage stage, WorkstreamArtefacts artefacts) =>
        (pipeline, stage) switch
        {
            (PipelineKind.Bug, PipelineStage.Spec) => BugArtefacts.HasRule(artefacts.FixText),
            (PipelineKind.Bug, PipelineStage.Implement) => BugArtefacts.HasFix(artefacts.FixText),
            _ => RecordFileIn(pipeline, stage, artefacts.Files) is not null,
        };

    /// <summary>
    /// The file that records a stage in this folder: <see cref="RecordFileOf"/>, or the latest numbered one of the same
    /// name, such as review-pr2.md or review-2.md for review.md; null when there is none. Two reviews kept as review-pr1.md
    /// and review-pr2.md showed Review as "not started" (walkthrough row 28). Spec and Verify take only their own name,
    /// because the gate and the evidence are read from spec.md and evidence.md alone; and a word suffix such as
    /// plan-old.md is not a record.
    /// </summary>
    public static string? RecordFileIn(PipelineKind pipeline, PipelineStage stage, IReadOnlySet<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var name = RecordFileOf(pipeline, stage);

        if (files.Contains(name) || stage is PipelineStage.Spec or PipelineStage.Verify)
        {
            return files.Contains(name) ? name : null;
        }

        var numbered = new System.Text.RegularExpressions.Regex(
            $@"^{System.Text.RegularExpressions.Regex.Escape(Path.GetFileNameWithoutExtension(name))}-(pr)?(?<n>[0-9]{{1,6}})\.md$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        return files
            .Select(file => (File: file, Match: numbered.Match(file)))
            .Where(each => each.Match.Success)
            .OrderBy(each => int.Parse(each.Match.Groups["n"].Value, CultureInfo.InvariantCulture))
            .LastOrDefault().File;
    }

    private static StageGate Gate(PipelineKind pipeline, PipelineStage stage, IReadOnlyList<PipelineStage> reached,
        WorkstreamArtefacts artefacts, EvidenceBlock? evidence, SpecAcceptance? acceptance)
    {
        var isReached = reached.Contains(stage);
        var laterReached = reached.Any(other => other > stage);

        if (stage == PipelineStage.Spec)
        {
            return SpecGate(pipeline, isReached, laterReached, artefacts, acceptance);
        }

        if (stage == PipelineStage.Verify)
        {
            return VerifyGate(isReached, laterReached, evidence);
        }

        if (stage == PipelineStage.Compound)
        {
            return new StageGate(stage, isReached ? GateState.Passed : GateState.NotReached, false);
        }

        return new StageGate(stage, (isReached, laterReached) switch
        {
            (true, true) => GateState.Passed,
            (true, false) => GateState.Waiting,
            (false, true) => GateState.Skipped,
            _ => GateState.NotReached,
        }, false);
    }

    private static StageGate SpecGate(PipelineKind pipeline, bool isReached, bool laterReached, WorkstreamArtefacts artefacts,
        SpecAcceptance? acceptance)
    {
        // A bug's rule is written only when the fix needs one (bug.yaml), so a fix with no rule passed no gate of his.
        if (!isReached)
        {
            var skipped = pipeline == PipelineKind.Bug && laterReached;

            return new StageGate(PipelineStage.Spec, skipped ? GateState.Skipped : GateState.NotReached, !skipped);
        }

        var current = pipeline == PipelineKind.Bug ? artefacts.RuleHash : artefacts.SpecHash;

        var state = acceptance switch
        {
            null => GateState.Waiting,
            _ when string.Equals(acceptance.ContentHash, current, StringComparison.OrdinalIgnoreCase) => GateState.Passed,
            _ => GateState.ChangedSinceAccepted,
        };

        return new StageGate(PipelineStage.Spec, state, true,
            state == GateState.ChangedSinceAccepted ? "changed since accepted" : null);
    }

    private static StageGate VerifyGate(bool isReached, bool laterReached, EvidenceBlock? evidence)
    {
        if (!isReached)
        {
            return laterReached
                ? new StageGate(PipelineStage.Verify, GateState.NoEvidence, false, "no evidence")
                : new StageGate(PipelineStage.Verify, GateState.NotReached, false);
        }

        if (evidence is null)
        {
            return new StageGate(PipelineStage.Verify, GateState.NoEvidence, false, "no evidence");
        }

        if (evidence.Result is null)
        {
            return new StageGate(PipelineStage.Verify, GateState.NoEvidence, false, $"no evidence: {evidence.Problem}");
        }

        return evidence.Passes
            ? new StageGate(PipelineStage.Verify, GateState.Passed, false)
            : new StageGate(PipelineStage.Verify, GateState.Waiting, false, $"evidence result: {evidence.Result}");
    }
}
