using System.Diagnostics;
using System.Text.Json;
using Theseus.Core.Persistence;

namespace Theseus.Core.Status;

/// <summary>Reads and writes <c>%LOCALAPPDATA%\Theseus\status.json</c>, and says whether its process still runs.</summary>
public static class TheseusStatusFile
{
    /// <summary>Where the status is kept.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", "status.json");

    /// <summary>The status as written, or null when there is none or it cannot be read.</summary>
    public static TheseusStatus? Read(string? path = null)
    {
        try
        {
            var text = AtomicFile.TryReadAllText(path ?? DefaultPath);

            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<TheseusStatus>(text, TheseusJson.Readable);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Writes the status. A failure is ignored: the status is advice, never a gate.</summary>
    public static void Write(TheseusStatus status, string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            AtomicFile.WriteAllText(target, JsonSerializer.Serialize(status, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Whether the process that wrote a status is still Theseus. A process id is reused once its owner
    /// has gone, so the name is checked as well.
    /// </summary>
    public static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            return !process.HasExited && process.ProcessName.Equals("Theseus", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
