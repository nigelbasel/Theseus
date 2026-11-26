namespace Theseus.Core.Locks;

/// <summary>One installed lock tool: where it is, whether it is there, and whether it is this build's.</summary>
public sealed record LockToolFile(string Name, string Path, bool IsInstalled, bool IsUpToDate, bool CanUndo);
