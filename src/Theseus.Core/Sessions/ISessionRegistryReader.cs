namespace Theseus.Core.Sessions;

/// <summary>Rebuilds the live view of Claude Code sessions from disk.</summary>
public interface ISessionRegistryReader
{
    /// <summary>Reads every registry entry and confirms each one against a running process.</summary>
    SessionRegistrySnapshot Read();
}
