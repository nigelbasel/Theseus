namespace Theseus.Core.Launching;

/// <summary>One running launcher, as Theseus finds it at start-up.</summary>
/// <param name="ProcessId">The launcher's process.</param>
/// <param name="ImagePath">The executable it runs from.</param>
/// <param name="StartedUtc">When it started.</param>
/// <param name="HasChild">Whether a process it started is still running: its session, while there is one.</param>
public sealed record LauncherProcess(int ProcessId, string ImagePath, DateTimeOffset StartedUtc, bool HasChild);
