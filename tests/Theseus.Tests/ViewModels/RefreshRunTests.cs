using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Refresh shows it is working (board-small-changes item 1, Nigel, 2026-11-26): busy, with a status line, while the board
/// is read again and the update check answers, and it cannot be pressed twice.
/// </summary>
[TestFixture]
public class RefreshRunTests
{
    private readonly List<string> _said = [];
    private string _notice = string.Empty;
    private int _refreshes;
    private TaskCompletionSource<string> _check = new();

    [SetUp]
    public void SetUp()
    {
        _said.Clear();
        _notice = string.Empty;
        _refreshes = 0;
        _check = new TaskCompletionSource<string>();
    }

    private RefreshRun Run(Action? refresh = null) => new(refresh ?? (() => _refreshes++), () => _check.Task, () => _notice,
        text =>
        {
            _notice = text;
            _said.Add(text);
        }, () => Task.CompletedTask);

    [Test]
    public void WhileTheCheckIsOut_ItIsBusy_SaysRefreshing_AndCannotBePressedAgain()
    {
        var run = Run();

        run.Command.Execute(null);

        Assert.That((run.IsBusy, run.Command.CanExecute(null), _said), Is.EqualTo((true, false, new List<string> { RefreshRun.Started })));
    }

    [Test]
    public async Task OnceTheCheckAnswers_ItIsNoLongerBusy_AndSaysWhatItFound()
    {
        var run = Run();
        var running = run.RunAsync();

        _check.SetResult("Theseus 2026.11.27.1 is available (you have 2026.11.26.1).");
        await running;

        Assert.That((run.IsBusy, run.Command.CanExecute(null)), Is.EqualTo((false, true)));
        Assert.That(_notice, Is.EqualTo("Refreshed. Theseus 2026.11.27.1 is available (you have 2026.11.26.1)."));
    }

    [Test]
    public async Task ASecondPressWhileBusy_RefreshesNothingMore()
    {
        var run = Run();
        var first = run.RunAsync();

        await run.RunAsync();
        _check.SetResult("Theseus is up to date.");
        await first;

        Assert.That(_refreshes, Is.EqualTo(1));
    }

    [Test]
    public async Task WhatTheRefreshItselfSaid_IsKept_AndTheFindingFollowsIt()
    {
        var run = Run(() => _notice = "A project can no longer be read.");
        var running = run.RunAsync();

        _check.SetResult("Theseus is up to date.");
        await running;

        Assert.That(_notice, Is.EqualTo("A project can no longer be read. Refreshed. Theseus is up to date."));
    }

    [Test]
    public void ARefreshThatThrows_LeavesItNoLongerBusy()
    {
        var run = Run(() => throw new InvalidOperationException("broken"));

        Assert.ThrowsAsync<InvalidOperationException>(run.RunAsync);
        Assert.That(run.IsBusy, Is.False);
    }

    [Test]
    public void IsBusy_IsRaised_SoTheWindowCanShowTheBusyCursor()
    {
        var run = Run();
        var raised = new List<string?>();
        run.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        run.Command.Execute(null);

        Assert.That(raised, Does.Contain(nameof(RefreshRun.IsBusy)));
    }
}
