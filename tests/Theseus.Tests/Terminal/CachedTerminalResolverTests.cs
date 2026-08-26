using Theseus.Core.Abstractions;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class CachedTerminalResolverTests
{
    private const string Stable = @"C:\Program Files\WindowsApps\WindowsTerminal.exe";

    private CountingResolver _inner = null!;
    private MovableClock _clock = null!;
    private CachedTerminalResolver _resolver = null!;

    [SetUp]
    public void SetUp()
    {
        _inner = new CountingResolver(new TerminalResolution(Stable, TerminalSource.RunningInstance, 1));
        _clock = new MovableClock(new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero));
        _resolver = new CachedTerminalResolver(_inner, _clock, TimeSpan.FromSeconds(15));
    }

    [Test]
    public void Resolve_EnumeratesProcessesOnceForRepeatedAsks()
    {
        for (var ask = 0; ask < 50; ask++)
        {
            _resolver.Resolve();
        }

        Assert.That(_inner.Calls, Is.EqualTo(1));
    }

    [Test]
    public void Resolve_ReturnsTheSameAnswerWhileItIsCached()
    {
        var first = _resolver.Resolve();
        _clock.Advance(TimeSpan.FromSeconds(14));

        Assert.That(_resolver.Resolve(), Is.SameAs(first));
    }

    [Test]
    public void Resolve_LooksAgainOnceTheCacheHasExpired()
    {
        _resolver.Resolve();
        _clock.Advance(TimeSpan.FromSeconds(16));
        _resolver.Resolve();

        Assert.That(_inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public void Resolve_PicksUpATerminalStartedAfterTheCacheExpired()
    {
        _inner.Answer = new TerminalResolution(string.Empty, TerminalSource.None, 0);
        Assert.That(_resolver.Resolve().Source, Is.EqualTo(TerminalSource.None));

        _inner.Answer = new TerminalResolution(Stable, TerminalSource.RunningInstance, 1);
        _clock.Advance(TimeSpan.FromSeconds(16));

        Assert.That(_resolver.Resolve().Source, Is.EqualTo(TerminalSource.RunningInstance));
    }

    private sealed class CountingResolver : ITerminalResolver
    {
        public CountingResolver(TerminalResolution answer) => Answer = answer;

        public TerminalResolution Answer { get; set; }

        public int Calls { get; private set; }

        public TerminalResolution Resolve()
        {
            Calls++;

            return Answer;
        }
    }

    private sealed class MovableClock : ISystemClock
    {
        public MovableClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
