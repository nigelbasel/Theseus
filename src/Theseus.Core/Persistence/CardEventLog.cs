using System.Text.Json;
using Theseus.Core.Domain;

namespace Theseus.Core.Persistence;

/// <summary>
/// What was done to cards that is not a run of their own, such as a project's session pausing one (1.1e AC 16): an
/// append-only <c>card-events.jsonl</c>, read for a card's Properties. A line that cannot be written is dropped rather
/// than taking the board down, as the run log does.
/// </summary>
public sealed class CardEventLog
{
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Creates the log over its file.</summary>
    public CardEventLog(string path) => _path = path;

    /// <summary>Adds an event.</summary>
    public void Append(CardEvent cardEvent)
    {
        ArgumentNullException.ThrowIfNull(cardEvent);

        var directory = Path.GetDirectoryName(_path);

        try
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (_gate)
            {
                File.AppendAllText(_path, JsonSerializer.Serialize(cardEvent, TheseusJson.Compact) + Environment.NewLine);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Diagnostics.TheseusLog.Warning(exception, "Could not record a card event in {Path}", _path);
        }
    }

    /// <summary>A card's events, oldest first; a line that does not read is skipped.</summary>
    public IReadOnlyList<CardEvent> For(string cardKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardKey);

        var text = AtomicFile.TryReadAllText(_path);

        if (text is null)
        {
            return [];
        }

        var events = new List<CardEvent>();

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (JsonSerializer.Deserialize<CardEvent>(line, TheseusJson.Compact) is { } read
                    && string.Equals(read.CardKey, cardKey, StringComparison.OrdinalIgnoreCase))
                {
                    events.Add(read);
                }
            }
            catch (JsonException)
            {
            }
        }

        return events;
    }
}
