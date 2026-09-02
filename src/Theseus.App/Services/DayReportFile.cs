using System.IO;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>
/// Where the day's report is kept: one file per day under Documents, so it is a permanent record
/// rather than something on a screen.
///
/// It lives in Documents and not in the application's own data directory on purpose. This is the
/// user's record of their work — it should be somewhere they would think to look, somewhere backed
/// up, and somewhere that survives Theseus being uninstalled.
/// </summary>
public static class DayReportFile
{
    /// <summary>The folder the reports are kept in.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Theseus", "Reports");

    /// <summary>The file one day's report belongs in.</summary>
    public static string PathFor(DateOnly day) => Path.Combine(Folder, $"{day:yyyy-MM-dd}.md");

    /// <summary>
    /// Writes the report, replacing the day's previous one. Rewriting is right rather than appending:
    /// a day's report is a statement about that day, and running it twice should not double it.
    /// </summary>
    /// <returns>Where it was written, or null if it could not be.</returns>
    public static string? Write(DateOnly day, string markdown)
    {
        var path = PathFor(day);

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(path, markdown);

            TheseusLog.Information("Wrote the report for {Day} to {Path}", day, path);

            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The report for {Day} could not be written to {Path}", day, path);

            return null;
        }
    }

    /// <summary>Every day already reported on, newest first, for looking back.</summary>
    public static IReadOnlyList<DateOnly> Written()
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                return [];
            }

            return
            [
                .. Directory.EnumerateFiles(Folder, "*.md")
                    .Select(file => DateOnly.TryParseExact(
                        Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", out var day) ? day : (DateOnly?)null)
                    .OfType<DateOnly>()
                    .OrderByDescending(day => day)
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
