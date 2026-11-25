namespace Theseus.Core.Running;

/// <summary>What became of a <see cref="RunRequest"/>: <see cref="RunStarted"/>, <see cref="RunQueued"/> or <see cref="RunRefused"/>.</summary>
public abstract record RunRequestOutcome;
