using Theseus.Core.Domain;

namespace Theseus.Core.Abstractions;

/// <summary>Reads and writes the board state that the plan format has no room for.</summary>
public interface IBoardStore
{
    /// <summary>Reads every sidecar, returning an empty list when the file is absent or unreadable.</summary>
    IReadOnlyList<WorkstreamSidecar> Read();

    /// <summary>Replaces the whole file atomically.</summary>
    void Write(IReadOnlyList<WorkstreamSidecar> sidecars);
}
