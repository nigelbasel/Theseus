using Theseus.Core.Domain;

namespace Theseus.Core.Abstractions;

/// <summary>The append-only history of launch attempts.</summary>
public interface IRunLog
{
    /// <summary>Appends one run.</summary>
    void Append(SessionRun run);

    /// <summary>Appends the ending of a run already recorded.</summary>
    void AppendEnd(RunEnd end);

    /// <summary>
    /// Reads every run that can still be understood, oldest first, with each ending already folded
    /// onto the run it belongs to. Callers never see the two shapes of line the file holds.
    /// </summary>
    IReadOnlyList<SessionRun> Read();
}
