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

        Write(JsonSerializer.Serialize(run, TheseusJson.Compact));
    }

    /// <inheritdoc />
    public void AppendEnd(RunEnd end)
    {
        ArgumentNullException.ThrowIfNull(end);

        Write(JsonSerializer.Serialize(end, TheseusJson.Compact));
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
        var endings = new Dictionary<Guid, RunEnd>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            Take(trimmed, runs, endings);
        }

        return [.. runs.Select(run => Apply(run, endings))];
    }

    /// <summary>
    /// Reads one line as whichever shape it is. The two are told apart by <c>runId</c>, which only an
    /// ending carries: a discriminator field written beside the data could contradict it, whereas a
    /// property that is either there or not cannot.
    /// </summary>
    private void Take(string line, List<SessionRun> runs, Dictionary<Guid, RunEnd> endings)
    {
        try
        {
            using var document = JsonDocument.Parse(line);

            if (document.RootElement.TryGetProperty("runId", out _))
            {
                if (JsonSerializer.Deserialize<RunEnd>(line, TheseusJson.Compact) is { } end)
                {
                    endings.TryAdd(end.RunId, end);
                }

                return;
            }

            if (JsonSerializer.Deserialize<SessionRun>(line, TheseusJson.Compact) is { Id: var id } run
                && id != Guid.Empty)
            {
                runs.Add(run);

                return;
            }

            TheseusLog.Warning("Skipped a line in {Path} that is neither a run nor an ending", _path);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning("Skipped an unreadable line in {Path}: {Reason}", _path, exception.Message);
        }
    }

    private static SessionRun Apply(SessionRun run, Dictionary<Guid, RunEnd> endings) =>
        endings.TryGetValue(run.Id, out var end)
            ? run with { EndedUtc = end.EndedUtc, ExitCode = end.ExitCode, ExitReason = end.Reason }
            : run;

    private void Write(string line)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (_gate)
        {
            File.AppendAllText(_path, line + Environment.NewLine, Encoding);
        }
    }
}
