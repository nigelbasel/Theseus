using System.Text;
using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Attention;

/// <summary>
/// <c>work.jsonl</c>: every start and stop a session reports, one per line, appended and never
/// rewritten. Written from the session time server, which serves several sessions at once, so every
/// append is serialised; a line this build cannot read is skipped rather than failing the file.
/// </summary>
public sealed class WorkLog
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _gate = new();

    /// <summary>Creates a log over a file.</summary>
    public WorkLog(string filePath) => FilePath = filePath;

    /// <summary>Where the log is.</summary>
    public string FilePath { get; }

    /// <summary>Adds a mark to the end of the file.</summary>
    public void Append(WorkMark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);

        var line = JsonSerializer.Serialize(mark, TheseusJson.Compact) + "\n";

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
            TheseusLog.Warning(exception, "Could not record work on {Issue} to {Path}", mark.Issue, FilePath);
        }
    }

    /// <summary>Every mark in the file that can be read, in the order written.</summary>
    public IReadOnlyList<WorkMark> Read()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var marks = new List<WorkMark>();

        foreach (var line in text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            try
            {
                if (JsonSerializer.Deserialize<WorkMark>(line, TheseusJson.Compact) is { } mark)
                {
                    marks.Add(mark);
                }
            }
            catch (JsonException exception)
            {
                TheseusLog.Debug("Skipped a work line that could not be read: {Reason}", exception.Message);
            }
        }

        return marks;
    }
}
