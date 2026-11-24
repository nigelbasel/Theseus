namespace Theseus.Core.Breakers;

/// <summary>The thresholds at which a card's circuit breakers trip; each a setting with a stated default (objective 2.2).</summary>
/// <param name="Crashes">Runs in a row that crashed or failed to start.</param>
/// <param name="NoProgress">Runs in a row that ended with no commit and no record written.</param>
/// <param name="SameError">Runs in a row that failed with the same first error line.</param>
/// <param name="OverTimeGraceMinutes">How long past its time limit a run may go before it is interrupted and the breaker trips.</param>
public sealed record BreakerSettings(int Crashes = 3, int NoProgress = 3, int SameError = 3, int OverTimeGraceMinutes = 15)
{
    /// <summary>The defaults a first run starts from.</summary>
    public static BreakerSettings Default { get; } = new();
}
