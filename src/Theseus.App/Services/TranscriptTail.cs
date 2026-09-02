using System.IO;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>
/// How much of a session's transcript would be replayed if it were resumed: the bytes after its last
/// compaction.
///
/// The file is read once and the answer remembered against its length, because these run to tens of
/// megabytes and the board refreshes every few seconds. A transcript that has not changed cannot have
/// a different answer, and one that has grown gets measured again.
/// </summary>
public static class TranscriptTail
{
    /// <summary>What a resume would work through, and how big the whole transcript is.</summary>
    /// <param name="TailBytes">Bytes after the last compaction.</param>
    /// <param name="TotalBytes">Bytes in the whole file.</param>
    public readonly record struct Measurement(long TailBytes, long TotalBytes);

    private const string Boundary = "\"subtype\":\"compact_boundary\"";

    private static readonly Dictionary<string, (long Length, long Tail)> Measured = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>
    /// The bytes a resume would replay, or zero when there is no transcript to measure. A session
    /// that has never been compacted replays all of it, which is the honest answer and not a fault.
    /// </summary>
    public static Measurement Measure(string? sessionId)
    {
        if (Find(sessionId) is not { } path)
        {
            return default;
        }

        try
        {
            var length = new FileInfo(path).Length;

            lock (Gate)
            {
                if (Measured.TryGetValue(path, out var cached) && cached.Length == length)
                {
                    return new Measurement(cached.Tail, length);
                }
            }

            var tail = TailOf(path, length);

            lock (Gate)
            {
                Measured[path] = (length, tail);
            }

            return new Measurement(tail, length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("The transcript at {Path} could not be measured: {Reason}", path, exception.Message);

            return default;
        }
    }

    /// <summary>
    /// Where the last compaction ends. Read forwards rather than sought backwards: the marker is one
    /// line among millions and its position is not known until it has been passed, and reading a
    /// 31 MB file once every time it changes is cheaper than being clever about it.
    /// </summary>
    private static long TailOf(string path, long length)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file);

        long consumed = 0;
        long afterBoundary = 0;

        while (reader.ReadLine() is { } line)
        {
            // The line's own bytes plus its newline. Counted rather than taken from the stream's
            // position, which runs ahead of the reader by however much it has buffered.
            consumed += System.Text.Encoding.UTF8.GetByteCount(line) + 1;

            if (line.Contains(Boundary, StringComparison.Ordinal))
            {
                afterBoundary = consumed;
            }
        }

        return Math.Max(0, length - afterBoundary);
    }

    private static string? Find(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var projects = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

        try
        {
            return Directory.Exists(projects)
                ? Directory.EnumerateFiles(projects, $"{sessionId}.jsonl", SearchOption.AllDirectories)
                    .FirstOrDefault()
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
