using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Terminal;

/// <summary>
/// Remembers which terminal was resolved for a short while. Resolution enumerates every process on
/// the machine, and the answer is read to decide whether a Start button is enabled, which the user
/// interface asks many times a second.
/// </summary>
public sealed class CachedTerminalResolver : ITerminalResolver
{
    private readonly ITerminalResolver _inner;
    private readonly ISystemClock _clock;
    private readonly TimeSpan _lifetime;

    private TerminalResolution? _cached;
    private DateTimeOffset _expiresUtc = DateTimeOffset.MinValue;
    private string? _reported;

    /// <summary>Creates a resolver that caches the inner resolver's answer.</summary>
    public CachedTerminalResolver(ITerminalResolver inner, ISystemClock clock, TimeSpan? lifetime = null)
    {
        _inner = inner;
        _clock = clock;
        _lifetime = lifetime ?? TimeSpan.FromSeconds(15);
    }

    /// <summary>Drops the remembered answer, so the next launch resolves afresh.</summary>
    public void Forget() => _cached = null;

    /// <inheritdoc />
    public TerminalResolution Resolve()
    {
        if (_cached is { } cached && _clock.UtcNow < _expiresUtc)
        {
            return cached;
        }

        var resolution = _inner.Resolve();

        _cached = resolution;
        _expiresUtc = _clock.UtcNow + _lifetime;

        Report(resolution);

        return resolution;
    }

    private void Report(TerminalResolution resolution)
    {
        var summary = $"{resolution.Source}:{resolution.ExecutablePath}";

        if (string.Equals(summary, _reported, StringComparison.Ordinal))
        {
            return;
        }

        _reported = summary;

        if (resolution.Source == TerminalSource.None)
        {
            TheseusLog.Error(new InvalidOperationException("No terminal found"),
                "No Windows Terminal could be found, so no session can be started");

            return;
        }

        if (resolution.Source == TerminalSource.RefusedFallback)
        {
            TheseusLog.Warning("Launching tabs through the wt.exe alias at {Path}: Windows refused to start the terminal package's own executable",
                resolution.ExecutablePath);

            return;
        }

        if (resolution.Source == TerminalSource.OnlyPackageAlias)
        {
            TheseusLog.Information("Launching tabs through the wt.exe alias at {Path}, the only terminal package installed",
                resolution.ExecutablePath);

            return;
        }

        if (resolution.Source == TerminalSource.ExecutionAlias)
        {
            TheseusLog.Warning(
                "Falling back to the wt.exe alias at {Path}. Tabs may open in a different package to the one hosting the existing windows",
                resolution.ExecutablePath);

            return;
        }

        TheseusLog.Information("Launching tabs into {Path} ({Packages} terminal package(s) running)",
            resolution.ExecutablePath, resolution.DistinctRunningPackages);
    }
}
