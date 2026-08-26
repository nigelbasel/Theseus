using System.Diagnostics;

namespace Theseus.Core.Abstractions;

/// <inheritdoc />
public sealed class ProcessHandle : IProcessHandle
{
    private readonly Process _process;

    /// <summary>Wraps an already-started process.</summary>
    public ProcessHandle(Process process) => _process = process;

    /// <inheritdoc />
    public int ProcessId => _process.Id;

    /// <inheritdoc />
    public int WaitForExit()
    {
        _process.WaitForExit();
        return _process.ExitCode;
    }

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();
}
