namespace Theseus.Core.Abstractions;

/// <summary>Starts child processes. Faked in tests so an argument vector can be asserted without executing it.</summary>
public interface IProcessLauncher
{
    /// <summary>Starts the process described by <paramref name="request"/>.</summary>
    IProcessHandle Start(ProcessStartRequest request);
}
