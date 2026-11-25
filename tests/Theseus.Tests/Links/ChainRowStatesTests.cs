using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>Each Chains row carries one mark and word, the first that applies (chains-panel-at-scale criterion 12).</summary>
[TestFixture]
public class ChainRowStatesTests
{
    private static readonly ChainRowFacts Stopped = new(SessionLifecycle.NotStarted);

    [TestCase(SessionLifecycle.NeedsAttention, ChainRowState.AskingYou)]
    [TestCase(SessionLifecycle.Launching, ChainRowState.Starting)]
    [TestCase(SessionLifecycle.Running, ChainRowState.Running)]
    [TestCase(SessionLifecycle.Idle, ChainRowState.Idle)]
    [TestCase(SessionLifecycle.NotStarted, ChainRowState.Stopped)]
    [TestCase(SessionLifecycle.Ended, ChainRowState.Stopped)]
    [TestCase(SessionLifecycle.Ghost, ChainRowState.Stopped)]
    public void TheSession_GivesTheState(SessionLifecycle lifecycle, ChainRowState expected) =>
        Assert.That(ChainRowStates.Of(new ChainRowFacts(lifecycle)), Is.EqualTo(expected));

    [Test]
    public void AReportWaiting_IsReported() =>
        Assert.That(ChainRowStates.Of(Stopped with { ReportWaiting = true }), Is.EqualTo(ChainRowState.Reported));

    [Test]
    public void AReportLost_IsReportLost() =>
        Assert.That(ChainRowStates.Of(Stopped with { ReportLost = true }), Is.EqualTo(ChainRowState.ReportLost));

    [Test]
    public void AStartWaitingForAllow_IsStartAsked() =>
        Assert.That(ChainRowStates.Of(Stopped with { StartAsked = true, IsPaused = true }), Is.EqualTo(ChainRowState.StartAsked));

    [Test]
    public void PausedWithNoSession_IsPaused() =>
        Assert.That(ChainRowStates.Of(Stopped with { IsPaused = true }), Is.EqualTo(ChainRowState.Paused));

    [Test]
    public void BlockedWithNoSession_IsBlocked() =>
        Assert.That(ChainRowStates.Of(Stopped with { IsBlocked = true }), Is.EqualTo(ChainRowState.Blocked));

    [Test]
    public void DoneWithNoSession_IsDone() =>
        Assert.That(ChainRowStates.Of(Stopped with { IsDone = true }), Is.EqualTo(ChainRowState.Done));

    [Test]
    public void ReadyWithNoSession_IsReady() =>
        Assert.That(ChainRowStates.Of(Stopped with { Readiness = CardReadiness.Ready }), Is.EqualTo(ChainRowState.Ready));

    [Test]
    public void WaitingOnInputs_IsWaitingOnInputs() =>
        Assert.That(ChainRowStates.Of(Stopped with { Readiness = CardReadiness.Waiting }), Is.EqualTo(ChainRowState.WaitingOnInputs));

    [Test]
    public void Archived_IsArchived_WhateverElseIsTrue() =>
        Assert.That(ChainRowStates.Of(Stopped with { IsArchived = true, IsDone = true }), Is.EqualTo(ChainRowState.Archived));

    [Test]
    public void AskingTheOwner_OutranksAReportWaiting() =>
        Assert.That(ChainRowStates.Of(new ChainRowFacts(SessionLifecycle.NeedsAttention) { ReportWaiting = true }),
            Is.EqualTo(ChainRowState.AskingYou));

    [Test]
    public void ASession_OutranksPausedAndDone() =>
        Assert.That(ChainRowStates.Of(new ChainRowFacts(SessionLifecycle.Running) { IsPaused = true, IsDone = true }),
            Is.EqualTo(ChainRowState.Running));

    [Test]
    public void AReportWaiting_OutranksARunningSession() =>
        Assert.That(ChainRowStates.Of(new ChainRowFacts(SessionLifecycle.Running) { ReportWaiting = true }),
            Is.EqualTo(ChainRowState.Reported));

    [Test]
    public void EveryState_HasAMarkAndAWord()
    {
        foreach (var state in Enum.GetValues<ChainRowState>())
        {
            Assert.That(ChainRowStates.Mark(state), Is.Not.Empty, state.ToString());
            Assert.That(ChainRowStates.Word(state), Is.Not.Empty, state.ToString());
        }
    }

    [Test]
    public void TheBoardsSessionMarks_AreReused()
    {
        Assert.That(ChainRowStates.Mark(ChainRowState.Running), Is.EqualTo("●"));
        Assert.That(ChainRowStates.Mark(ChainRowState.Idle), Is.EqualTo("○"));
        Assert.That(ChainRowStates.Mark(ChainRowState.AskingYou), Is.EqualTo("◆"));
        Assert.That(ChainRowStates.Word(ChainRowState.AskingYou), Is.EqualTo("asking you"));
    }

    [TestCase(SessionLifecycle.Launching, true)]
    [TestCase(SessionLifecycle.Running, true)]
    [TestCase(SessionLifecycle.Idle, true)]
    [TestCase(SessionLifecycle.NeedsAttention, true)]
    [TestCase(SessionLifecycle.NotStarted, false)]
    [TestCase(SessionLifecycle.Ended, false)]
    public void ActiveIsASession_AsOnTheBoard(SessionLifecycle lifecycle, bool active) =>
        Assert.That(ChainRowStates.IsActive(new ChainRowFacts(lifecycle)), Is.EqualTo(active));

    [Test]
    public void ActiveIsAlsoSomethingWaitingOnTheOwner() =>
        Assert.That(ChainRowStates.IsActive(Stopped with { ReportWaiting = true }), Is.True);

    [TestCase(true, false, false, false, true)]
    [TestCase(false, true, false, false, true)]
    [TestCase(false, false, true, false, true)]
    [TestCase(false, false, false, true, true)]
    [TestCase(false, false, false, false, false)]
    public void WaitingOnTheOwner_IsAQuestionAReportOrAStart(bool asking, bool reported, bool lost, bool startAsked, bool waits) =>
        Assert.That(ChainRowStates.WaitsOnOwner(new ChainRowFacts(asking ? SessionLifecycle.NeedsAttention : SessionLifecycle.NotStarted)
        {
            ReportWaiting = reported,
            ReportLost = lost,
            StartAsked = startAsked,
        }), Is.EqualTo(waits));

    [Test]
    public void Completed_IsDoneOrArchived_WithNoSessionAndNothingWaiting()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChainRowStates.IsCompleted(Stopped with { IsDone = true }), Is.True, "done");
            Assert.That(ChainRowStates.IsCompleted(Stopped with { IsArchived = true }), Is.True, "archived");
            Assert.That(ChainRowStates.IsCompleted(Stopped), Is.False, "merely stopped (Nigel, 2026-11-25)");
            Assert.That(ChainRowStates.IsCompleted(Stopped with { IsPaused = true }), Is.False, "paused");
            Assert.That(ChainRowStates.IsCompleted(new ChainRowFacts(SessionLifecycle.Idle) { IsDone = true }), Is.False, "done but running");
            Assert.That(ChainRowStates.IsCompleted(Stopped with { IsDone = true, ReportWaiting = true }), Is.False, "a report waits");
        });
    }
}
