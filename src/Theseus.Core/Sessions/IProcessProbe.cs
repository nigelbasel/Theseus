namespace Theseus.Core.Sessions;

/// <summary>Asks the operating system whether a process is still alive, and when it started.</summary>
public interface IProcessProbe
{
    /// <summary>
    /// Returns the process creation time as a file time string, or <see langword="null"/> when no
    /// process with that id is running.
    /// </summary>
    string? GetStartFileTime(int pid);
}
