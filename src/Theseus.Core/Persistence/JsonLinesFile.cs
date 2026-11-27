using System.Text;
using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Persistence;

/// <summary>
/// A file of one JSON object per line, appended and never rewritten, for the records 7.3 adds (gaps, method breaks, piece
/// events). Appends are serialised, so it is safe from the MCP server's threads; a line this build cannot read is skipped
/// rather than failing the file, and a write that fails is logged rather than thrown, because losing one record must not
/// stop the work it records.
/// </summary>
/// <typeparam name="T">What each line holds.</typeparam>
public sealed class JsonLinesFile<T>
    where T : class
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _gate = new();

    /// <summary>Creates a file over a path.</summary>
    public JsonLinesFile(string filePath) => FilePath = filePath;

    /// <summary>Where the file is.</summary>
    public string FilePath { get; }

    /// <summary>Adds records to the end of the file.</summary>
    public void Append(params IReadOnlyList<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return;
        }

        var text = string.Concat(records.Select(record => JsonSerializer.Serialize(record, TheseusJson.Compact) + "\n"));

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
            TheseusLog.Warning(exception, "Could not write {Count} record(s) to {Path}", records.Count, FilePath);
        }
    }

    /// <summary>Every record in the file that can be read, in the order written.</summary>
    public IReadOnlyList<T> Read()
    {
        string? text;

        lock (_gate)
        {
            text = AtomicFile.TryReadAllText(FilePath);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var records = new List<T>();

        foreach (var line in text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            try
            {
                if (JsonSerializer.Deserialize<T>(line, TheseusJson.Compact) is { } record)
                {
                    records.Add(record);
                }
            }
            catch (JsonException exception)
            {
                TheseusLog.Debug("Skipped a line of {Path} that could not be read: {Reason}", FilePath, exception.Message);
            }
        }

        return records;
    }
}
