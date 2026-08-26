using Theseus.Core.Domain;

namespace Theseus.Core.Abstractions;

/// <summary>The append-only history of launch attempts.</summary>
public interface IRunLog
{
    /// <summary>Appends one run.</summary>
    void Append(SessionRun run);

    /// <summary>Reads every run that can still be understood, oldest first.</summary>
    IReadOnlyList<SessionRun> Read();
}
