using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Locks;

/// <summary>
/// <c>locks.json</c>: every ticket held or waiting, rewritten whole on each change, so a Theseus that
/// restarts resumes exactly where it stopped (Nigel, 2026-10-06). A file that cannot be read is logged
/// and treated as empty rather than failing the board.
/// </summary>
public sealed class LockStateStore
{
    /// <summary>Creates a store over a file.</summary>
    public LockStateStore(string filePath) => FilePath = filePath;

    /// <summary>Where the state is kept.</summary>
    public string FilePath { get; }

    /// <summary>Every ticket in the file, in queue order.</summary>
    public IReadOnlyList<LockTicket> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<LockTicket>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The lock state in {Path} could not be read, so every lock starts free", FilePath);

            return [];
        }
    }

    /// <summary>Replaces the file with these tickets.</summary>
    public void Save(IReadOnlyList<LockTicket> tickets)
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(tickets, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the lock state to {Path}", FilePath);
        }
    }
}
