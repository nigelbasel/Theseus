using Theseus.Core.Kanban;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Kanban;

/// <summary>The one column a piece of work sits in (projects-kanban spec AC 9, 10).</summary>
[TestFixture]
public class KanbanPlacementTests
{
    private static readonly (PipelineStage?, StageNodeState)[] FeatureAtSpec =
    [
        (PipelineStage.Research, StageNodeState.Done), (PipelineStage.Spec, StageNodeState.WaitingOnYou),
        (PipelineStage.Plan, StageNodeState.ToDo), (PipelineStage.Implement, StageNodeState.ToDo),
        (PipelineStage.Verify, StageNodeState.ToDo), (PipelineStage.Review, StageNodeState.ToDo),
        (PipelineStage.Compound, StageNodeState.ToDo),
    ];

    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void APiece_SitsAtItsFirstUnfinishedStage()
    {
        Assert.That(KanbanPlacement.ColumnOf(FeatureAtSpec, null, false, StageNodeState.WaitingOnYou), Is.EqualTo(KanbanColumn.Spec));
    }

    [Test]
    public void TheStageItsSessionNamed_Wins_WhileUnfinished()
    {
        Assert.That(KanbanPlacement.ColumnOf(FeatureAtSpec, PipelineStage.Implement, true, StageNodeState.Busy),
            Is.EqualTo(KanbanColumn.Build));
    }

    [Test]
    public void ANamedStageThatIsFinished_FallsThroughToTheFirstUnfinished()
    {
        Assert.That(KanbanPlacement.ColumnOf(FeatureAtSpec, PipelineStage.Research, true, StageNodeState.Busy),
            Is.EqualTo(KanbanColumn.Spec));
    }

    [Test]
    public void ANamedStageThePipelineLacks_IsIgnored()
    {
        (PipelineStage?, StageNodeState)[] bug =
            [(PipelineStage.Research, StageNodeState.Done), (PipelineStage.Implement, StageNodeState.ToDo)];

        Assert.That(KanbanPlacement.ColumnOf(bug, PipelineStage.Plan, true, StageNodeState.Busy), Is.EqualTo(KanbanColumn.Build));
    }

    [Test]
    public void ASkippedStage_IsPassed()
    {
        (PipelineStage?, StageNodeState)[] bug =
        [
            (PipelineStage.Research, StageNodeState.Done), (PipelineStage.Spec, StageNodeState.NotNeeded),
            (PipelineStage.Implement, StageNodeState.ToDo),
        ];

        Assert.That(KanbanPlacement.ColumnOf(bug, null, false, StageNodeState.ToDo), Is.EqualTo(KanbanColumn.Build));
    }

    [Test]
    public void EveryStageFinished_IsDone()
    {
        (PipelineStage?, StageNodeState)[] all = [(PipelineStage.Research, StageNodeState.Done), (PipelineStage.Implement, StageNodeState.NotNeeded)];

        Assert.That(KanbanPlacement.ColumnOf(all, null, false, StageNodeState.Busy), Is.EqualTo(KanbanColumn.Done));
    }

    [TestCase(StageNodeState.Done)]
    [TestCase(StageNodeState.Answered)]
    public void ADoneOrAnsweredStatus_IsDone_WhateverItsStages(StageNodeState status)
    {
        Assert.That(KanbanPlacement.ColumnOf(FeatureAtSpec, PipelineStage.Spec, true, status), Is.EqualTo(KanbanColumn.Done));
    }

    [Test]
    public void FreeformWork_IsBuild_UntilDone()
    {
        (PipelineStage?, StageNodeState)[] work = [(null, StageNodeState.Busy)];

        Assert.That(KanbanPlacement.ColumnOf(work, null, true, StageNodeState.Busy), Is.EqualTo(KanbanColumn.Build));
        Assert.That(KanbanPlacement.ColumnOf(work, null, true, StageNodeState.Done), Is.EqualTo(KanbanColumn.Done));
    }

    [Test]
    public void ABugWithItsFixWritten_SitsInVerify()
    {
        var piece = OnePiece(_folder, "bug", new Dictionary<string, string>
        {
            ["reproduction.md"] = "# Reproduction\n",
            ["fix.md"] = "# Fix\n\n## Cause\nThe cause.\n\n## Change\nThe change.\n",
        });

        Assert.That(KanbanPlacement.ColumnOf(piece), Is.EqualTo(KanbanColumn.Verify));
    }

    [Test]
    public void AFeatureWithNoRecords_SitsInResearch()
    {
        Assert.That(KanbanPlacement.ColumnOf(OnePiece(_folder, "feature")), Is.EqualTo(KanbanColumn.Research));
    }

    [Test]
    public void AFeatureWaitingOnItsSpec_SitsInSpec()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md", "spec.md"));

        Assert.That(KanbanPlacement.ColumnOf(piece), Is.EqualTo(KanbanColumn.Spec));
    }

    [Test]
    public void EveryColumn_HasAHeader_InOrder()
    {
        Assert.That(KanbanColumns.All.Select(KanbanColumns.Header), Is.EqualTo(new[]
        {
            "Research / Reproduce", "Spec / Rule", "Plan", "Build (Implement, Fix, Work)", "Verify", "Review", "Lessons", "Done",
        }));
    }
}
