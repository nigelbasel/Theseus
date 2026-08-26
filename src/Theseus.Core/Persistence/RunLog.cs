using System.Text;
using System.Text.Json;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;

namespace Theseus.Core.Persistence;

/// <summary>
/// One JSON object per line, appended and never rewritten. A line this build cannot read is skipped
/// rather than discarded, so an older or newer format costs history rather than the whole file.
/// </summary>
public sealed class RunLog : IRunLog
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Creates a log over a <c>runs.jsonl</c> file.</summary>
    public RunLog(string path) => _path = path;

    /// <inheritdoc />
    public void Append(SessionRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (_gate)
        {
            File.AppendAllText(_path, JsonSerializer.Serialize(run, TheseusJson.Compact) + Environment.NewLine, Encoding);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SessionRun> Read()
    {
        var text = AtomicFile.TryReadAllText(_path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var runs = new List<SessionRun>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize<SessionRun>(trimmed, TheseusJson.Compact) is { } run)
                {
                    runs.Add(run);
                }
            }
            catch (JsonException exception)
            {
                TheseusLog.Warning("Skipped an unreadable line in {Path}: {Reason}", _path, exception.Message);
            }
        }

        return runs;
    }
}
