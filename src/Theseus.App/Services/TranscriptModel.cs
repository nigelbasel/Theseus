using System.IO;
using System.Text;
using Theseus.Core.Diagnostics;
using Theseus.Core.Sessions;

namespace Theseus.App.Services;

/// <summary>
/// Which model a session is running, read off the end of its transcript.
///
/// Read from the end because that is where the answer is: the last real assistant turn. A live
/// session's transcript grows every turn, so this runs often, and reading tens of megabytes forward
/// every few seconds to reach the last line would be the wrong trade. The tail is tried first; only
/// a transcript whose last turns are buried under an enormous tool result falls back to a full read.
///
/// Remembered against the file's length, like the restart cost: a file that has not grown cannot
/// have a different answer.
/// </summary>
public static class TranscriptModel
{
    private const int TailBytes = 512 * 1024;

    private static readonly Dictionary<string, (long Length, string Model)> Known = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>The model's friendly name, or empty when there is no transcript or no real turn in it.</summary>
    public static string Read(string? sessionId)
    {
        if (TranscriptTail.Find(sessionId) is not { } path)
        {
            return string.Empty;
        }

        try
        {
            var length = new FileInfo(path).Length;

            lock (Gate)
            {
                if (Known.TryGetValue(path, out var cached) && cached.Length == length)
                {
                    return cached.Model;
                }
            }

            var model = ModelName.Friendly(LastModel(path, length));

            lock (Gate)
            {
                Known[path] = (length, model);
            }

            return model;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("The transcript at {Path} could not be read for its model: {Reason}", path, exception.Message);

            return string.Empty;
        }
    }

    /// <summary>The raw model id on the last real turn. Internal so the tail-then-fallback logic can be tested on a file of any shape.</summary>
    internal static string? LastModel(string path, long length)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (FromTail(file, length) is { } found)
        {
            return found;
        }

        file.Position = 0;

        return FromWholeFile(file);
    }

    /// <summary>The last real turn within the final half-megabyte, scanning lines from the end.</summary>
    private static string? FromTail(FileStream file, long length)
    {
        var start = Math.Max(0, length - TailBytes);
        var buffer = new byte[length - start];

        file.Position = start;
        file.ReadExactly(buffer);

        var text = Encoding.UTF8.GetString(buffer);
        var lines = text.Split('\n');

        // The first piece is a fragment when the window did not start on a line boundary.
        for (var index = lines.Length - 1; index >= (start == 0 ? 0 : 1); index--)
        {
            if (ModelName.FromTranscriptLine(lines[index]) is { } model)
            {
                return model;
            }
        }

        return null;
    }

    private static string? FromWholeFile(FileStream file)
    {
        using var reader = new StreamReader(file, Encoding.UTF8, leaveOpen: true);
        string? last = null;

        while (reader.ReadLine() is { } line)
        {
            if (ModelName.FromTranscriptLine(line) is { } model)
            {
                last = model;
            }
        }

        return last;
    }
}
