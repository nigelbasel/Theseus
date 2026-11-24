using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Duties;

/// <summary>
/// <c>duties.json</c> in Theseus's data folder: the store of every duty on every card, rewritten whole on each change so
/// duties survive a restart of Theseus or of the machine (Nigel, 2026-11-24). A file that cannot be read is logged and
/// treated as empty.
/// </summary>
public sealed class DutyStore
{
    /// <summary>Creates a store over a file.</summary>
    public DutyStore(string filePath) => FilePath = filePath;

    /// <summary>Where the duties are kept.</summary>
    public string FilePath { get; }

    /// <summary>What the file holds.</summary>
    public DutyFile Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return DutyFile.Empty;
        }

        try
        {
            var file = JsonSerializer.Deserialize<DutyFile>(text, TheseusJson.Readable);

            return file is null ? DutyFile.Empty : new DutyFile(file.Duties ?? [], file.Queue ?? []);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The duties in {Path} could not be read, so none runs", FilePath);

            return DutyFile.Empty;
        }
    }

    /// <summary>Replaces the file.</summary>
    public void Save(DutyFile file)
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(file, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the duties to {Path}", FilePath);
        }
    }
}
