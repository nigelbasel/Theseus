namespace Theseus.Core.Locks;

/// <summary>Finds the test hosts running under a process.</summary>
public interface IProcessTree
{
    /// <summary>
    /// Every test host descended from <paramref name="processId"/>: a <c>testhost</c> or
    /// <c>vstest.console</c> image, or <c>dotnet</c> running one. Build servers are never included,
    /// because other sessions' builds share them.
    /// </summary>
    IReadOnlyList<HeldProcess> TestHostsUnder(int processId);
}
