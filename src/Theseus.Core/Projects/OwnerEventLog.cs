using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>owner-events.jsonl</c>: what the owner did on the board, by the card it concerns (1.1e AC 27a), one line each,
/// appended; and, beside it, when each card's session last called events(), so it is given only what is new.
/// </summary>
public sealed class OwnerEventLog
{
    private readonly string _readPath;
    private readonly Lock _gate = new();

    /// <summary>Creates the log over a file; the read marks go beside it.</summary>
    public OwnerEventLog(string filePath)
    {
        FilePath = filePath;
        _readPath = Path.ChangeExtension(filePath, ".read.json");
    }

    /// <summary>Where the events are kept.</summary>
    public string FilePath { get; }

    /// <summary>Adds an event.</summary>
    public void Add(OwnerEvent owned)
    {
        ArgumentNullException.ThrowIfNull(owned);

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, JsonSerializer.Serialize(owned, TheseusJson.Compact) + Environment.NewLine);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                TheseusLog.Warning(exception, "An owner event for {Card} could not be written to {Path}", owned.CardKey, FilePath);
            }
        }
    }

    /// <summary>
    /// The card's events since its session last asked, oldest first, and marks them read; on the first call, every event.
    /// </summary>
    public IReadOnlyList<OwnerEvent> TakeNew(string cardKey, DateTimeOffset now)
    {
        lock (_gate)
        {
            var marks = Marks();
            var since = marks.GetValueOrDefault(cardKey);
            var events = All().Where(owned => string.Equals(owned.CardKey, cardKey, StringComparison.OrdinalIgnoreCase) && owned.AtUtc > since)
                .ToList();
            marks[cardKey] = now;

            try
            {
                AtomicFile.WriteAllText(_readPath, JsonSerializer.Serialize(marks, TheseusJson.Readable));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                TheseusLog.Warning(exception, "The owner event marks could not be written to {Path}", _readPath);
            }

            return events;
        }
    }

    /// <summary>Every event, oldest first; a line that does not read is skipped.</summary>
    public IReadOnlyList<OwnerEvent> All()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var events = new List<OwnerEvent>();

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (JsonSerializer.Deserialize<OwnerEvent>(line, TheseusJson.Readable) is { } owned)
                {
                    events.Add(owned);
                }
            }
            catch (JsonException)
            {
                // A line cut off by a crash mid-write is skipped; the rest still read.
            }
        }

        return events;
    }

    private Dictionary<string, DateTimeOffset> Marks()
    {
        try
        {
            return AtomicFile.TryReadAllText(_readPath) is { Length: > 0 } text
                ? JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(text, TheseusJson.Readable)
                  is { } read ? new Dictionary<string, DateTimeOffset>(read, StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
