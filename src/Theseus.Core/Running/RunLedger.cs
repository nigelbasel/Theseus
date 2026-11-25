using System.Text;
using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Running;

/// <summary>
/// <c>run-ledger.jsonl</c>: one JSON object per ended run, appended and never rewritten. A line this build cannot read is
/// skipped and logged, so a damaged line costs one run's record, not the budgets.
/// </summary>
public sealed class RunLedger : IRunLedger
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Creates the ledger over a file.</summary>
    public RunLedger(string path) => _path = path;

    /// <inheritdoc />
    public void Append(RunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var line = JsonSerializer.Serialize(record, TheseusJson.Compact) + "\n";

        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.AppendAllText(_path, line, Encoding);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RunRecord> Read()
    {
        string? text;

        lock (_gate)
        {
            text = AtomicFile.TryReadAllText(_path);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var records = new List<RunRecord>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize<RunRecord>(trimmed, TheseusJson.Compact) is { } record)
                {
                    records.Add(record);
                }
            }
            catch (JsonException exception)
            {
                TheseusLog.Warning(exception, "Skipped a line of the run ledger {Path} that could not be read", _path);
            }
        }

        return records;
    }
}
