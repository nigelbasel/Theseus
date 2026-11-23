using System.Globalization;

namespace Theseus.Core.Projects;

/// <summary>
/// The independent check run without the owner's press (git-22, 2026-11-23: nothing Theseus makes waits on him): a verifier
/// for each waiting build a scenario names, once its own session has stopped; one scenario writer for the builds none
/// names, while none stands; and words for what is under way. Start the checks… stays, as a run again by hand.
/// </summary>
public static class CheckSteps
{
    private static readonly string[] Numbers = ["no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];

    /// <summary>The steps for a project's pieces.</summary>
    /// <param name="pieces">Its pieces, as <see cref="PiecesOfWork.Build"/> gave them.</param>
    /// <param name="isNamed">Whether a scenario's Checks line names a piece.</param>
    /// <param name="writerStands">Whether the project has a scenario writer that is not archived. One that finished without
    /// naming every build is started again, never made again, so a writer that cannot finish its work is not made in a loop.</param>
    /// <param name="writerRunning">Whether that writer's session is running or starting.</param>
    /// <param name="canWrite">Whether the project's holdout folder can be written: one chosen but missing is the owner's to set.</param>
    /// <param name="idleWriter">A standing writer that is neither running nor starting, or null: started rather than left waiting.</param>
    /// <param name="isChecked">Whether a build's verifier has reported: it stays waiting until its own records say so, and is
    /// not checked again, however often Theseus restarts.</param>
    /// <param name="isHeld">Whether the owner held a build: paused, blocked or stopped by him. It gets no verifier.</param>
    public static CheckPlan For(IReadOnlyList<PieceOfWork> pieces, Func<PieceOfWork, bool> isNamed, bool writerStands, bool writerRunning,
        bool canWrite, string? idleWriter = null, Func<PieceOfWork, bool>? isChecked = null, Func<PieceOfWork, bool>? isHeld = null)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(isNamed);

        var waiting = pieces.Where(piece => piece is { IsCurrentPiece: true, Card: not null }
                                            && piece.Status.State == StageNodeState.HoldoutsNotRun).ToList();
        var notChecked = waiting.Where(piece => piece.PendingVerifiers.Count == 0 && isChecked?.Invoke(piece) != true).ToList();
        var unnamed = notChecked.Count(piece => !isNamed(piece));
        var checking = waiting.Count(piece => piece.PendingVerifiers.Any(verifier => verifier.HasSession || verifier.IsStarting));

        var underway = new List<string>();

        if (writerRunning && unnamed > 0)
        {
            underway.Add($"Writing scenarios for {Count(unnamed, "build")}…");
        }

        if (checking > 0)
        {
            underway.Add($"Checking {Count(checking, "build")}…");
        }

        return new CheckPlan(
            [.. notChecked.Where(piece => isNamed(piece) && piece.Card is { IsWorking: false } && isHeld?.Invoke(piece) != true)
                .Select(piece => piece.CardKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)],
            unnamed > 0 && !writerStands && canWrite,
            underway,
            unnamed > 0 && writerStands && !writerRunning && canWrite ? idleWriter : null);
    }

    private static string Count(int count, string one) =>
        $"{(count < Numbers.Length ? Numbers[count] : count.ToString(CultureInfo.InvariantCulture))} {(count == 1 ? one : one + "s")}";
}
