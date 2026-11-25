using Theseus.App.ViewModels;
using Theseus.Core.Duties;
using Theseus.Tests.Duties;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The duties checklist acts on the ticked rows and says what each action did, so a press is never silent (Nigel, live
/// check 2026-11-25: "I did not see any feedback when I ticked run now and pause").
/// </summary>
[TestFixture]
public class DutiesViewModelTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
    private string _path = string.Empty;
    private DutyBook _book = null!;
    private readonly List<string> _recorded = [];

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"theseus-duties-vm-{Guid.NewGuid():N}.json");
        _book = new DutyBook(new DutyStore(_path), new DutyTestClock(Nine), TimeZoneInfo.Utc);
        _recorded.Clear();
        _book.Set("card", "Live check", "reply", DutyCadence.Every(TimeSpan.FromMinutes(5)), DutyEnd.Never, null);
    }

    [TearDown]
    public void TearDown() => File.Delete(_path);

    private DutiesViewModel Model() =>
        new(new DutiesActions(_book, key => key == "card" ? "Card" : key, (card, text) => _recorded.Add(text), _ => false, _ => true,
            [("card", "Card")], () => { }, (_, _) => []), "the board", _ => true);

    [Test]
    public void Pause_PausesTheTickedDuty_AndSaysSo()
    {
        var model = Model();
        model.Rows.Single().IsTicked = true;

        model.PauseCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_book.Find("card", "Live check")!.PausedBy, Is.EqualTo(Duty.Owner));
            Assert.That(model.Notice, Is.EqualTo("Paused: Live check. They do not run until resumed."));
            Assert.That(model.Rows.Single().IsTicked, Is.True, "the row stays ticked across the reload");
            Assert.That(model.Rows.Single().Detail, Does.StartWith("⏸ Live check · paused"));
        });
    }

    [Test]
    public void RunNow_AsksForARun_AndSaysWhenItWillGo()
    {
        var model = Model();
        model.Rows.Single().IsTicked = true;

        model.RunNowCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_book.Find("card", "Live check")!.State.RunNowRequested, Is.True);
            Assert.That(model.Notice, Is.EqualTo("Run now: Live check runs as soon as Card allows."));
            Assert.That(_recorded, Is.EqualTo(new[] { "Duty \"Live check\" run now by you" }));
        });
    }

    [Test]
    public void Resume_ResumesIt_AndSaysSo()
    {
        var model = Model();
        model.Rows.Single().IsTicked = true;
        model.PauseCommand.Execute(null);

        model.ResumeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_book.Find("card", "Live check")!.IsPaused, Is.False);
            Assert.That(model.Notice, Is.EqualTo("Resumed: Live check."));
        });
    }
}
