using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Links;

/// <summary>
/// <c>%LOCALAPPDATA%\Theseus\holdout-folders.json</c> (1.1e AC 6e): the holdout folders in force, one per project and one
/// for ad hoc cards, written by Theseus whenever they change, so the holdout guard protects exactly what the board uses.
/// A session never writes it: the guard refuses the file tools there.
/// </summary>
public static class HoldoutFolderFile
{
    /// <summary>The file's name in Theseus's data folder.</summary>
    public const string FileName = "holdout-folders.json";

    private sealed record Listed(IReadOnlyList<HoldoutFolderEntry>? Folders);

    /// <summary>Where it is.</summary>
    public static string PathIn(string? localAppData = null) =>
        Path.Combine(localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", FileName);

    /// <summary>The folders listed, or none when the file is missing or does not read.</summary>
    public static IReadOnlyList<HoldoutFolderEntry> Read(string path)
    {
        try
        {
            return AtomicFile.TryReadAllText(path) is { Length: > 0 } text
                   && JsonSerializer.Deserialize<Listed>(text, TheseusJson.Readable) is { Folders: { } folders }
                ? [.. folders.OfType<HoldoutFolderEntry>().Where(entry => !string.IsNullOrWhiteSpace(entry.Folder))]
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Writes the folders in force, with every folder listed before kept (review of PR 7, finding 5): a folder leaves the
    /// guard's care only when the owner says, never because a convention changed, a card went or a project file was
    /// edited. Whether it wrote.
    /// </summary>
    public static bool Write(string path, IReadOnlyList<HoldoutFolderEntry> inForce)
    {
        ArgumentNullException.ThrowIfNull(inForce);
        var folders = inForce.Concat(Read(path).Where(before => !inForce.Any(now =>
                string.Equals(Path.TrimEndingDirectorySeparator(now.Folder), Path.TrimEndingDirectorySeparator(before.Folder),
                    StringComparison.OrdinalIgnoreCase))))
            .ToList();
        var text = JsonSerializer.Serialize(new Listed(folders), TheseusJson.Readable);

        if (string.Equals(AtomicFile.TryReadAllText(path), text, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteAllText(path, text);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The holdout folders could not be written to {Path}", path);

            return false;
        }
    }
}
