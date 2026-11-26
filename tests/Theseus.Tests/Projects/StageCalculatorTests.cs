using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Stages and gates from a slug's artefacts (spec AC 6 to 9). Fed the artefacts as the reader hands them over, so these
/// test the rules alone; <see cref="WorkstreamArtefactReaderTests"/> tests reading them with git.
/// </summary>
[TestFixture]
public class StageCalculatorTests
{
    private static readonly WorkstreamSource OnBranch = new("work/a-slug");

    private const string PassingEvidence = "Prose.\n\n```theseus-evidence\n{ \"result\": \"pass\" }\n```\n";

    private static WorkstreamArtefacts Artefacts(params string[] files) =>
        new("a-slug", OnBranch, files.ToHashSet()) { SpecHash = files.Contains("spec.md") ? "spec-now" : null };

    private static WorkstreamStage Feature(WorkstreamArtefacts artefacts, SpecAcceptance? acceptance = null) =>
        StageCalculator.Calculate([PipelineKind.Feature], artefacts, acceptance);

    private static WorkstreamStage Bug(WorkstreamArtefacts artefacts, SpecAcceptance? acceptance = null) =>
        StageCalculator.Calculate([PipelineKind.Bug], artefacts, acceptance);

    private static SpecAcceptance Accepted(string hash) => new("a-project", "a-slug", hash, DateTimeOffset.UnixEpoch);

    [TestCase("research.md", PipelineStage.Research)]
    [TestCase("spec.md", PipelineStage.Spec)]
    [TestCase("plan.md", PipelineStage.Plan)]
    [TestCase("implementation.md", PipelineStage.Implement)]
    [TestCase("evidence.md", PipelineStage.Verify)]
    [TestCase("review.md", PipelineStage.Review)]
    [TestCase("lessons.md", PipelineStage.Compound)]
    public void AFeatureStage_IsReached_WhenItsArtefactExists(string file, PipelineStage stage)
    {
        Assert.That(Feature(Artefacts(file)).Reached, Is.EqualTo(new[] { stage }));
    }

    [TestCase("review-pr1.md", PipelineStage.Review)]
    [TestCase("plan-2.md", PipelineStage.Plan)]
    [TestCase("lessons-pr2.md", PipelineStage.Compound)]
    public void AStage_IsReached_ByANumberedOrSuffixedRecordOfItsName(string file, PipelineStage stage)
    {
        // Walkthrough row 28: two reviews kept as review-pr1.md and review-pr2.md showed Review as "not started".
        Assert.That(Feature(Artefacts(file)).Reached, Is.EqualTo(new[] { stage }));
    }

    [TestCase("plan-old.md")]
    [TestCase("research-notes.md")]
    [TestCase("spec-2.md")]
    [TestCase("spec-notes.md")]
    [TestCase("evidence-draft.md")]
    [TestCase("evidence-2.md")]
    public void AWordSuffix_OrANumberedSpecOrEvidence_IsNotARecord(string file)
    {
        // Review of P1, should-fix 3: spec-notes.md put up an Accept spec gate for a spec.md that was not there.
        Assert.That(Feature(Artefacts(file)).Reached, Is.Empty);
    }

    [Test]
    public void TheRecordOpened_IsTheExactName_ElseTheFirstOfItsNumberedOnes_ElseNone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StageCalculator.RecordFileIn(PipelineKind.Feature, PipelineStage.Review, new HashSet<string> { "review.md", "review-pr1.md" }), Is.EqualTo("review.md"));
            Assert.That(StageCalculator.RecordFileIn(PipelineKind.Feature, PipelineStage.Review, new HashSet<string> { "review-pr2.md", "review-pr10.md", "review-pr1.md" }), Is.EqualTo("review-pr10.md"), "the latest, by number");
            Assert.That(StageCalculator.RecordFileIn(PipelineKind.Feature, PipelineStage.Review, new HashSet<string> { "reviewer-notes.md" }), Is.Null, "a different word is not a review");
        });
    }

    [Test]
    public void ABugsStages_SitUnderTheFeatureColumns_FromReproductionAndFixSections()
    {
        var reproduced = Bug(Artefacts("reproduction.md"));
        var ruled = Bug(Artefacts("reproduction.md", "fix.md") with { FixText = "# x\n\n## Spec (the rule)\n1. A rule.\n" });
        var fixedBug = Bug(Artefacts("reproduction.md", "fix.md") with { FixText = "## Spec\nRule.\n\n## Cause\nWhy.\n" });

        Assert.That(reproduced.Reached, Is.EqualTo(new[] { PipelineStage.Research }));
        Assert.That(ruled.Reached, Is.EqualTo(new[] { PipelineStage.Research, PipelineStage.Spec }));
        Assert.That(fixedBug.Reached, Is.EqualTo(new[] { PipelineStage.Research, PipelineStage.Spec, PipelineStage.Implement }));
    }

    [Test]
    public void ABugsChangeHeading_AlsoMeansItsFixIsWritten()
    {
        var stage = Bug(Artefacts("reproduction.md", "fix.md") with { FixText = "## Change\nWhat changed.\n" });

        Assert.That(stage.Reached, Does.Contain(PipelineStage.Implement));
    }

    [Test]
    public void ABug_HasNoPlanStage_AndNoPlanGate()
    {
        var stage = Bug(Artefacts("reproduction.md", "plan.md"));

        Assert.That(stage.Reached, Is.EqualTo(new[] { PipelineStage.Research }));
        Assert.That(stage.GateAfter(PipelineStage.Plan), Is.Null);
    }

    [Test]
    public void AReachedSpec_WaitsOnTheOwner_UntilHeAcceptsIt()
    {
        var stage = Feature(Artefacts("research.md", "spec.md"));

        Assert.That(stage.GateAfter(PipelineStage.Spec), Is.EqualTo(new StageGate(PipelineStage.Spec, GateState.Waiting, true)));
        Assert.That(stage.IsWaitingOnOwner, Is.True);
    }

    [Test]
    public void AnAcceptedSpec_HasPassed()
    {
        var stage = Feature(Artefacts("research.md", "spec.md"), Accepted("spec-now"));

        Assert.That(stage.GateAfter(PipelineStage.Spec)!.State, Is.EqualTo(GateState.Passed));
        Assert.That(stage.IsWaitingOnOwner, Is.False);
    }

    [Test]
    public void ASpecChangedSinceItWasAccepted_IsWaitingAgain()
    {
        var stage = Feature(Artefacts("research.md", "spec.md"), Accepted("spec-before"));

        Assert.That(stage.GateAfter(PipelineStage.Spec),
            Is.EqualTo(new StageGate(PipelineStage.Spec, GateState.ChangedSinceAccepted, true, "changed since accepted")));
        Assert.That(stage.IsWaitingOnOwner, Is.True);
    }

    [Test]
    public void ABugsRule_IsItsSpec_ComparedByTheRuleAlone()
    {
        var artefacts = Artefacts("reproduction.md", "fix.md") with { FixText = "## Spec\nRule.\n", RuleHash = "rule-now" };

        Assert.That(Bug(artefacts).GateAfter(PipelineStage.Spec)!.State, Is.EqualTo(GateState.Waiting));
        Assert.That(Bug(artefacts, Accepted("rule-now")).GateAfter(PipelineStage.Spec)!.State, Is.EqualTo(GateState.Passed));
    }

    [Test]
    public void ABugFixedWithNoRule_SkippedTheRule_AndPassedNoGateOfTheOwners()
    {
        var stage = Bug(Artefacts("reproduction.md", "fix.md") with { FixText = "## Cause\nWhy.\n" });

        Assert.That(stage.GateAfter(PipelineStage.Spec), Is.EqualTo(new StageGate(PipelineStage.Spec, GateState.Skipped, false)));
        Assert.That(stage.Position, Is.EqualTo(PipelineStage.Implement));
        Assert.That(stage.Note, Is.Null);
    }

    [Test]
    public void OtherStages_PassWhenALaterStageIsReached()
    {
        var stage = Feature(Artefacts("research.md", "spec.md", "plan.md", "implementation.md"), Accepted("spec-now"));

        Assert.That(stage.GateAfter(PipelineStage.Research)!.State, Is.EqualTo(GateState.Passed));
        Assert.That(stage.GateAfter(PipelineStage.Plan)!.State, Is.EqualTo(GateState.Passed),
            "plan is an evidence gate inside a project from method-v0.2, not the owner's");
        Assert.That(stage.GateAfter(PipelineStage.Implement)!.State, Is.EqualTo(GateState.Waiting));
        Assert.That(stage.GateAfter(PipelineStage.Verify)!.State, Is.EqualTo(GateState.NotReached));
    }

    [Test]
    public void Verify_PassesOnlyOnAPassResult_InTheLastBlock()
    {
        var failingThenPassing = "```theseus-evidence\n{ \"result\": \"fail\" }\n```\n\n" + PassingEvidence;
        var passingThenOpen = PassingEvidence + "\n```theseus-evidence\n{ \"result\": \"open\" }\n```\n";

        Assert.That(Verify(failingThenPassing), Is.EqualTo(new StageGate(PipelineStage.Verify, GateState.Passed, false)));
        Assert.That(Verify(passingThenOpen),
            Is.EqualTo(new StageGate(PipelineStage.Verify, GateState.Waiting, false, "evidence result: open")));
    }

    [Test]
    public void Verify_WithAnEvidenceFileButNoBlock_ShowsNoEvidence_NeverPassed()
    {
        Assert.That(Verify("Only prose: 1762 of 1762 passed."),
            Is.EqualTo(new StageGate(PipelineStage.Verify, GateState.NoEvidence, false, "no evidence")));
    }

    [Test]
    public void Verify_WithABlockThatIsNotJson_ShowsNoEvidence_WithWhy()
    {
        var gate = Verify("```theseus-evidence\n{ result: pass\n```\n");

        Assert.That(gate.State, Is.EqualTo(GateState.NoEvidence));
        Assert.That(gate.Note, Does.StartWith("no evidence: the last theseus-evidence block is not valid JSON"));
    }

    [Test]
    public void APassedReview_WithNoEvidenceFile_ShowsVerifyAsNoEvidence()
    {
        var stage = Feature(Artefacts("spec.md", "implementation.md", "review.md"), Accepted("spec-now"));

        Assert.That(stage.GateAfter(PipelineStage.Verify)!.State, Is.EqualTo(GateState.NoEvidence));
    }

    [Test]
    public void TheEvidenceBlocksGateClaims_AreInformationOnly_AndPassNoGate()
    {
        const string claiming = "```theseus-evidence\n{ \"result\": \"open\", \"gates\": [ { \"gate\": \"spec\", \"accepted\": \"2026-11-19T21:30:00Z\" } ] }\n```\n";

        var stage = Feature(Artefacts("spec.md", "evidence.md") with { EvidenceText = claiming });

        Assert.That(stage.EvidenceClaims, Is.EqualTo(new[] { new EvidenceGateClaim("spec", "2026-11-19T21:30:00Z") }));
        Assert.That(stage.GateAfter(PipelineStage.Spec)!.State, Is.EqualTo(GateState.Waiting),
            "a session can write the block, so its claim is never proof (evidence-block Q3)");
    }

    [Test]
    public void ACard_SitsAtTheFurthestReachedStage()
    {
        var stage = Feature(Artefacts("research.md", "spec.md", "plan.md", "implementation.md"), Accepted("spec-now"));

        Assert.That(stage.Position, Is.EqualTo(PipelineStage.Implement));
        Assert.That(stage.Note, Is.Null);
    }

    [Test]
    public void WorkRunPastTheSpecGate_SitsAtSpec_SayingWhatWasWrittenAhead()
    {
        var stage = Feature(Artefacts("research.md", "spec.md", "plan.md"));

        Assert.That(stage.Position, Is.EqualTo(PipelineStage.Spec));
        Assert.That(stage.Note, Is.EqualTo("ahead of your gate: plan written"));
        Assert.That(stage.GateAfter(PipelineStage.Spec)!.State, Is.EqualTo(GateState.Waiting), "the waiting gate stays visible");
    }

    [Test]
    public void AChangedSpec_PullsTheCardBackToSpec()
    {
        var stage = Feature(Artefacts("spec.md", "plan.md", "implementation.md"), Accepted("spec-before"));

        Assert.That(stage.Position, Is.EqualTo(PipelineStage.Spec));
        Assert.That(stage.Note, Is.EqualTo("ahead of your gate: implementation written"));
    }

    [Test]
    public void WorkFromBeforeMethodV02_ShowsItsStage_WithNoGateMarkers()
    {
        var stage = Feature(Artefacts("research.md", "spec.md", "plan.md", "review.md") with { IsBeforeKit = true });

        Assert.That(stage.Position, Is.EqualTo(PipelineStage.Review));
        Assert.That(stage.Gates, Is.Empty);
        Assert.That(stage.Note, Is.EqualTo("before method-v0.2"));
    }

    [Test]
    public void AWorkstreamWithNoWorkFolder_HasNoStage()
    {
        var stage = Feature(WorkstreamArtefacts.NotStarted("a-slug"));

        Assert.That(stage.Position, Is.Null);
        Assert.That(stage.Note, Is.Null);
    }

    [Test]
    public void NothingWrittenYet_HasNoStage()
    {
        var stage = Feature(Artefacts());

        Assert.That((stage.HasStarted, stage.Position, stage.Note), Is.EqualTo((false, (PipelineStage?)null, (string?)null)));
    }

    [Test]
    public void FreeformWork_AndWorkWithNoPipelineChosen_HaveNoStages_UntilAGatesRecordIsWritten()
    {
        Assert.That(StageCalculator.Calculate([PipelineKind.Freeform], Artefacts("notes.md"), null).Position, Is.Null);
        Assert.That(StageCalculator.Calculate([], Artefacts("research.md"), null).Position, Is.Null);
    }

    [Test]
    public void ASpecWritten_UnderFreeformOrNoPipelineChosen_WaitsAtTheSpecGate()
    {
        // git-22 for Nigel, 2026-11-26: a spec.md written under such an objective must still show its Accept.
        Assert.That(StageCalculator.Calculate([PipelineKind.Freeform], Artefacts("spec.md"), null).IsWaitingOnOwner, Is.True);
        Assert.That(StageCalculator.Calculate([], Artefacts("spec.md"), null).IsWaitingOnOwner, Is.True);
    }

    [Test]
    public void WhereAnObjectiveNamesTwoPipelines_ASlugsArtefactsChoose()
    {
        PipelineKind[] both = [PipelineKind.Feature, PipelineKind.Bug];

        Assert.That(StageCalculator.PipelineFor(both, new HashSet<string> { "reproduction.md" }), Is.EqualTo(PipelineKind.Bug));
        Assert.That(StageCalculator.PipelineFor(both, new HashSet<string> { "fix.md" }), Is.EqualTo(PipelineKind.Bug));
        Assert.That(StageCalculator.PipelineFor(both, new HashSet<string> { "spec.md" }), Is.EqualTo(PipelineKind.Feature));
    }

    private static StageGate Verify(string evidence) =>
        Feature(Artefacts("spec.md", "implementation.md", "evidence.md") with { EvidenceText = evidence }, Accepted("spec-now"))
            .GateAfter(PipelineStage.Verify)!;
}
