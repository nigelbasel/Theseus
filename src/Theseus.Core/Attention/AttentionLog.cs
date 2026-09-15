using System.Text;
using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Attention;

/// <summary>
/// <c>attention.jsonl</c>: one closed interval per line, appended and never rewritten. Only closed
/// intervals are written, so the file holds a line per change of tab or session state rather than
/// a line per sample, and a line this build cannot read is skipped rather than failing the file.
/// </summary>
public sealed class AttentionLog
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _gate = new();

    /// <summary>Creates a log over a file.</summary>
    public AttentionLog(string filePath) => FilePath = filePath;

    /// <summary>Where the log is.</summary>
    public string FilePath { get; }

    /// <summary>Adds intervals to the end of the file.</summary>
    public void Append(IReadOnlyList<AttentionInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        if (intervals.Count == 0)
        {
            return;
        }

        var text = string.Concat(intervals.Select(interval => JsonSerializer.Serialize(interval, TheseusJson.Compact) + "\n"));

        try
        {
            lock (_gate)
            {
                if (Path.GetDirectoryName(FilePath) is { Length: > 0 } directory)
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(FilePath, text, Encoding);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not write {Count} attention interval(s) to {Path}", intervals.Count, FilePath);
        }
    }

    /// <summary>Every interval in the file that can be read.</summary>
    public IReadOnlyList<AttentionInterval> Read()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var intervals = new List<AttentionInterval>();

        foreach (var line in text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            try
            {
                if (JsonSerializer.Deserialize<AttentionInterval>(line, TheseusJson.Compact) is { } interval)
                {
                    intervals.Add(interval);
                }
            }
            catch (JsonException exception)
            {
                TheseusLog.Debug("Skipped an attention line that could not be read: {Reason}", exception.Message);
            }
        }

        return intervals;
    }
}
