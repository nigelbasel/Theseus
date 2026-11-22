using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>Works out a piece's kind, and the icon, word and meaning each kind is shown with (1.1f A1 to A3).</summary>
public static class PieceKinds
{
    /// <summary>What the kind word's hover adds after the kind's meaning (A3).</summary>
    public const string HowWorkedOut = "Worked out by Theseus from the pipeline and branch.";

    /// <summary>The first rule that applies: Verify, Bug, Freeform, Spec, otherwise Build (A1).</summary>
    /// <param name="role">The role of the card that works it.</param>
    /// <param name="pipeline">The pipeline it runs, or null when none is chosen.</param>
    /// <param name="branch">Its row's Branch, or null when no row lists it.</param>
    public static PieceKind Of(WorkstreamRole role, PipelineKind? pipeline, string? branch) => (role, pipeline) switch
    {
        (WorkstreamRole.Verifier, _) => PieceKind.Verify,
        (_, PipelineKind.Bug) => PieceKind.Bug,
        (_, null or PipelineKind.Freeform) => PieceKind.Freeform,
        _ when string.Equals(branch?.Trim(), "none", StringComparison.OrdinalIgnoreCase) => PieceKind.Spec,
        _ => PieceKind.Build,
    };

    /// <summary>The kind's icon.</summary>
    public static string Icon(PieceKind kind) => kind switch
    {
        PieceKind.Spec => "✎",
        PieceKind.Build => "⚒",
        PieceKind.Verify => "◎",
        PieceKind.Bug => "✱",
        _ => "○",
    };

    /// <summary>The kind's word, always shown beside its colour so colour is never the only signal (A2).</summary>
    public static string Word(PieceKind kind) => kind.ToString();

    /// <summary>What the kind means, for its hover (A3).</summary>
    public static string Means(PieceKind kind) => kind switch
    {
        PieceKind.Spec => "Works out what to build and hands the building on. Its branch is none, so it cannot change code.",
        PieceKind.Build => "Builds a change on its own branch, through spec, plan, build, check and review.",
        PieceKind.Verify => "An independent check: a fresh session runs the hidden scenarios against another piece's build. Only you start one.",
        PieceKind.Bug => "Fixes a fault: reproduce it, state the rule, fix it, check it.",
        _ => "Work with no fixed stages, such as an inventory or an access change. Done when its objective is done.",
    };
}
