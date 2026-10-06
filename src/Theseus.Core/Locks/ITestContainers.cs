namespace Theseus.Core.Locks;

/// <summary>The Testcontainers containers on this machine's Docker.</summary>
public interface ITestContainers
{
    /// <summary>Every container carrying the Testcontainers label, running or stopped.</summary>
    IReadOnlyList<TestContainer> List();

    /// <summary>Removes one container, stopping it first if it is running.</summary>
    void Remove(string id);
}
