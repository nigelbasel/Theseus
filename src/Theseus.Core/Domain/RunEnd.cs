namespace Theseus.Core.Domain;

/// <summary>
/// The ending of a run, recorded as a line of its own rather than by rewriting the line that
/// started it. The run log is append-only by design - crash-safe, needing no lock beyond the append,
/// and costing one record rather than the file when a line goes bad - and amending a record in place
/// would give all of that up to fill in two fields.
/// </summary>
/// <param name="RunId">The run this ends, which is what joins it to its start.</param>
/// <param name="EndedUtc">
/// When it ended, where that was observed. Null means the ending is known to have happened but its
/// moment is not: a run that finished while nothing was watching has ended, and inventing a time for
/// it would put a fabricated duration into a measurement.
/// </param>
/// <param name="ExitCode">Claude Code's exit status, where anything observed it.</param>
/// <param name="Reason">How it ended.</param>
public sealed record RunEnd(
    Guid RunId,
    DateTimeOffset? EndedUtc,
    int? ExitCode,
    SessionExitReason Reason);
