namespace Theseus.Core.Running;

/// <summary>A headless run's process as the runner sees it: its stdin, what it wrote last to stderr, and a way to end it.</summary>
public interface IHeadlessProcess : IHeadlessInput
{
    /// <summary>Its process id.</summary>
    int ProcessId { get; }

    /// <summary>The last line it wrote to stderr.</summary>
    string? LastError { get; }

    /// <summary>Ends it: only for a run that began no session, or would not exit after an interrupt.</summary>
    void End();
}
