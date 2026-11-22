namespace Theseus.Core.Projects;

/// <summary>
/// One row of a piece's Records view: a stage, what its document is for in plain words, the stage's state, and the
/// document when it is written (Nigel, 2026-11-22).
/// </summary>
/// <param name="Stage">The stage's name, such as "Spec" or "Fix"; for another file in the folder, its name.</param>
/// <param name="Purpose">What its document is for, such as "What will be built, for you to accept".</param>
/// <param name="State">The stage's state.</param>
/// <param name="File">The document's full path when it is written; null when it is not.</param>
public sealed record RecordEntry(string Stage, string Purpose, StageNodeState State, string? File)
{
    /// <summary>
    /// Its state in the Records view's words: written, accepted by you on a date, not written, skipped with its reason, or
    /// done with no record (30a).
    /// </summary>
    public string StateText { get; init; } = string.Empty;

    /// <summary>The file's name, shown small and muted; empty when none is written.</summary>
    public string FileName => File is null ? string.Empty : Path.GetFileName(File);

    /// <summary>Whether there is a document to open.</summary>
    public bool HasFile => File is not null;

    /// <summary>Whether it is another file of the folder, listed under "Other records".</summary>
    public bool IsOther { get; init; }
}
