using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Attention;

/// <summary>
/// <c>attention-open.json</c>: the intervals still open, rewritten every <see cref="Every"/> (7.3). Open intervals were
/// held in memory and written only on a clean exit, and 689 of 986 run ends were crashes or kills, so a crash lost all of
/// them. Now it loses at most a minute. The file is a separate one, rewritten whole, so <c>attention.jsonl</c> keeps one
/// line per closed interval and no line of it is ever rewritten.
/// </summary>
public sealed class AttentionCheckpoint
{
    /// <summary>How often what is open is written down.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(60);

    private DateTimeOffset? _written;

    /// <summary>Creates a checkpoint over a file.</summary>
    public AttentionCheckpoint(string filePath) => FilePath = filePath;

    /// <summary>Where it is kept.</summary>
    public string FilePath { get; }

    /// <summary>Whether a checkpoint is due at this moment.</summary>
    public bool IsDue(DateTimeOffset at) => _written is not { } written || at - written >= Every;

    /// <summary>Writes what is open now.</summary>
    public void Write(IReadOnlyList<AttentionInterval> open, DateTimeOffset at) => Save(new AttentionCheckpointState(at, Closed: false, open));

    /// <summary>Says the sampler stopped cleanly, everything it had open written to the log.</summary>
    public void Close(DateTimeOffset at) => Save(new AttentionCheckpointState(at, Closed: true, []));

    /// <summary>
    /// What the last run left: the intervals it had open, if it ended without closing them, and the stretch from its last
    /// checkpoint to now, in which nothing was measured. Each is given once: the file is rewritten as empty at once.
    /// </summary>
    /// <param name="now">The moment the sampler starts again.</param>
    /// <param name="offset">The UTC offset to stamp on the gap.</param>
    public (IReadOnlyList<AttentionInterval> Intervals, AttentionGap? Gap) Recover(DateTimeOffset now, string? offset)
    {
        var state = Load();

        if (state is null)
        {
            return ([], null);
        }

        Save(new AttentionCheckpointState(now, Closed: false, []));

        var gap = now > state.At
            ? new AttentionGap(state.At, now, state.Closed ? AttentionGap.NotRunning : AttentionGap.Crashed) { Offset = offset }
            : null;

        return (state.Closed ? [] : [.. state.Open.Where(interval => interval.End > interval.Start)], gap);
    }

    private AttentionCheckpointState? Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AttentionCheckpointState>(text, TheseusJson.Readable);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The attention checkpoint in {Path} could not be read; nothing is recovered from it", FilePath);

            return null;
        }
    }

    private void Save(AttentionCheckpointState state)
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(state, TheseusJson.Readable));
            _written = state.At;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not write the attention checkpoint to {Path}", FilePath);
        }
    }
}
