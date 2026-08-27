using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class LaneInsertionTests
{
    [Test]
    public void At_PutsACardFromAnotherLaneExactlyWhereItWasDropped()
    {
        Assert.That(LaneInsertion.At(chosen: 1, cameFrom: null, remaining: 3), Is.EqualTo(1));
    }

    [Test]
    public void At_AllowsForTheGapLeftBehindWhenACardMovesDownItsOwnLane()
    {
        Assert.That(LaneInsertion.At(chosen: 2, cameFrom: 0, remaining: 2), Is.EqualTo(1),
            "the position was measured with the card still in the lane, and lifting it shifts the rest up");
    }

    [Test]
    public void At_LeavesTheChosenPlaceAloneWhenACardMovesUpItsOwnLane()
    {
        Assert.That(LaneInsertion.At(chosen: 0, cameFrom: 2, remaining: 2), Is.EqualTo(0),
            "nothing above it moved, so the position it was dropped on still means what it meant");
    }

    [Test]
    public void At_LandsACardDraggedToTheVeryEndAtTheVeryEnd()
    {
        Assert.That(LaneInsertion.At(chosen: 3, cameFrom: 0, remaining: 2), Is.EqualTo(2),
            "dropping past the last card must actually move it, not leave it where it was");
    }

    [Test]
    public void At_NeverLandsBeyondTheLane()
    {
        Assert.That(LaneInsertion.At(chosen: 99, cameFrom: null, remaining: 2), Is.EqualTo(2));
        Assert.That(LaneInsertion.At(chosen: -5, cameFrom: null, remaining: 2), Is.Zero);
    }

    [Test]
    public void At_LeavesACardDroppedOnItselfWhereItIs()
    {
        Assert.That(LaneInsertion.At(chosen: 1, cameFrom: 1, remaining: 2), Is.EqualTo(1));
    }
}
