namespace Theseus.Core.Abstractions;

/// <summary>A started child process.</summary>
public interface IProcessHandle : IDisposable
{
    /// <summary>The child's process identifier.</summary>
    int ProcessId { get; }

    /// <summary>Blocks until the child exits and returns its exit code.</summary>
    int WaitForExit();
}
