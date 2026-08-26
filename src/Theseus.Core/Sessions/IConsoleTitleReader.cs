namespace Theseus.Core.Sessions;

/// <summary>Reads the title a running session has given its own console.</summary>
public interface IConsoleTitleReader
{
    /// <summary>The session's console title, or null when it cannot be read.</summary>
    string? Read(int processId);
}
