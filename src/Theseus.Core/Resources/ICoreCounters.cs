namespace Theseus.Core.Resources;

/// <summary>Reads the machine's logical processors: parked or not, and how busy.</summary>
public interface ICoreCounters : IDisposable
{
    /// <summary>The current reading, or null when there is none (the first read, or counters that cannot be read).</summary>
    CoreSample? Read();
}
