namespace Theseus.Core.Projects;

/// <summary>
/// One row of a piece's Records view: a stage, what its document is for in plain words, the stage's state, and the
/// document when it is written (brief, Records view, 2026-11-22).
/// </summary>
/// <param name="Stage">The stage's name, such as "Spec" or "Fix".</param>
/// <param name="Purpose">What its document is for, such as "What will be built, for you to accept".</param>
/// <param name="State">The stage's state.</param>
/// <param name="File">The document's full path when it is written; null when it is not.</param>
public sealed record RecordEntry(string Stage, string Purpose, StageNodeState State, string? File)
{
    /// <summary>The state's mark and word, such as "✓ Done".</summary>
    public string StateText => $"{StageNodeStates.Mark(State)} {StageNodeStates.Word(State)}";

    /// <summary>Whether there is a document to open.</summary>
    public bool HasFile => File is not null;
}
