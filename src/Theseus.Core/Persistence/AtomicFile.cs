using System.Text;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Persistence;

/// <summary>
/// Writes a file in one step from a reader's point of view. Four sessions can write plans inside
/// the same minute, and a half-flushed file that another process reads is otherwise guaranteed.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes <paramref name="contents"/> to <paramref name="path"/>, replacing it wholesale.</summary>
    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, contents, Encoding);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>
    /// Reads a file without blocking a concurrent writer, returning null if it is missing or locked.
    /// </summary>
    public static string? TryReadAllText(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: true);

            return reader.ReadToEnd();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException exception)
        {
            TheseusLog.Warning(exception, "Could not read {Path}, most likely because it is being written", path);

            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            TheseusLog.Warning(exception, "Not allowed to read {Path}", path);

            return null;
        }
    }
}
