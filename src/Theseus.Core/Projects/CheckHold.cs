using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// The Waits line for a project's built pieces whose independent check is held up by the project or by its scenario
/// writer rather than by the piece itself, in plain words with what clears it (Nigel, 2026-11-25: "9 not yet checked ...
/// how to I make the checks run"). A piece's own reason, such as a missing holdout folder, is said by the piece's line.
/// </summary>
public static class CheckHold
{
    private const string BreakerPrefix = "Breaker tripped: ";

    /// <summary>The line, or null when neither the project nor its writer holds a check up.</summary>
    /// <param name="project">The project's name, for the sentence.</param>
    /// <param name="projectStopped">Whether the owner stopped the project: Theseus then starts none of its checks.</param>
    /// <param name="waiting">The built pieces waiting for a check that has not run, none of them held by the owner.</param>
    /// <param name="isNamed">Whether a holdout scenario's Checks line names a piece.</param>
    /// <param name="writer">The project's standing scenario writer, or null.</param>
    /// <param name="writerHeld">Whether the writer is paused, blocked or stopped, by its breaker or the owner.</param>
    public static WaitItem? For(string project, bool projectStopped, IReadOnlyList<PieceOfWork> waiting, Func<PieceOfWork, bool> isNamed,
        Workstream? writer, bool writerHeld)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        ArgumentNullException.ThrowIfNull(isNamed);

        if (waiting.Count == 0)
        {
            return null;
        }

        if (projectStopped)
        {
            return new WaitItem(WaitKind.CheckCannotRun,
                $"{Head(waiting, "waits for its independent check", "wait for their independent check")}: {project} is stopped, so Theseus starts no checks. Start the project with ▶ on its card.",
                [.. waiting.Select(piece => piece.Name)]);
        }

        var unnamed = waiting.Where(piece => !isNamed(piece)).ToList();

        if (writer is null || !writerHeld || unnamed.Count == 0)
        {
            return null;
        }

        var them = unnamed.Count == 1 ? "it" : "them";
        var because = $"{Head(unnamed, "cannot be checked", "cannot be checked")}: no holdout scenario names {them}, and {writer.Title}, which writes {them},";
        IReadOnlyList<string> pieces = [.. unnamed.Select(piece => piece.Name)];

        return writer.Breaker is { } trip
            ? new WaitItem(WaitKind.ResetBreaker,
                $"{because} is stopped by its breaker ({Reason(trip.Detail)}). Reset its breaker and Theseus starts it again.", pieces)
            {
                CardKey = writer.Key,
                AndStart = true,
            }
            : new WaitItem(WaitKind.CheckCannotRun, $"{because} is paused. Start it from its card.", pieces);
    }

    private static string Head(IReadOnlyList<PieceOfWork> pieces, string one, string many) =>
        pieces.Count == 1
            ? $"{pieces[0].Title} {one}"
            : $"{pieces.Count} built pieces {many}, {string.Join(", ", pieces.Select(piece => piece.Title))}";

    private static string Reason(string detail) =>
        (detail.StartsWith(BreakerPrefix, StringComparison.Ordinal) ? detail[BreakerPrefix.Length..] : detail).TrimEnd('.');
}
