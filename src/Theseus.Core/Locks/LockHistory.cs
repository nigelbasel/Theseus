using System.Text;
using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Locks;

/// <summary>
/// <c>locks-history.jsonl</c>: every grant, release, drop and change Nigel makes, appended and never
/// rewritten, so what contention costs can be measured rather than guessed. Only Theseus writes it.
/// </summary>
public sealed class LockHistory
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _gate = new();

    /// <summary>Creates a history over a file.</summary>
    public LockHistory(string filePath) => FilePath = filePath;

    /// <summary>Where the history is.</summary>
    public string FilePath { get; }

    /// <summary>Adds an event to the end of the file.</summary>
    public void Append(LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        var line = JsonSerializer.Serialize(lockEvent, TheseusJson.Compact) + "\n";

        try
        {
            lock (_gate)
            {
                if (Path.GetDirectoryName(FilePath) is { Length: > 0 } directory)
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(FilePath, line, Encoding);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not record a {Kind} on the {Lock} lock to {Path}", lockEvent.Kind, lockEvent.Lock, FilePath);
        }
    }

    /// <summary>Every event in the file that can be read, in the order written.</summary>
    public IReadOnlyList<LockEvent> Read()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var events = new List<LockEvent>();

        foreach (var line in text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            try
            {
                if (JsonSerializer.Deserialize<LockEvent>(line, TheseusJson.Compact) is { } lockEvent)
                {
                    events.Add(lockEvent);
                }
            }
            catch (JsonException exception)
            {
                TheseusLog.Debug("Skipped a lock history line that could not be read: {Reason}", exception.Message);
            }
        }

        return events;
    }
}
