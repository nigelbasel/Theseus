using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>reported-acceptances.json</c>: what sessions reported the owner said in their tabs, waiting for him to confirm on
/// the board (1.1e AC 28). One per workstream: a later report replaces an earlier one. Kept in Theseus's data folder.
/// </summary>
public sealed class ReportedAcceptanceStore
{
    /// <summary>Creates a store over a file.</summary>
    public ReportedAcceptanceStore(string filePath) => FilePath = filePath;

    /// <summary>Where the reports are kept.</summary>
    public string FilePath { get; }

    /// <summary>Every report waiting, oldest first.</summary>
    public IReadOnlyList<ReportedAcceptance> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ReportedAcceptance>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The reported acceptances in {Path} could not be read", FilePath);

            return [];
        }
    }

    /// <summary>The report waiting for a workstream, if any.</summary>
    public ReportedAcceptance? For(string project, string slug) =>
        Load().LastOrDefault(report => Matches(report, project, slug));

    /// <summary>Keeps a report, replacing any earlier one for the same workstream.</summary>
    public void Report(ReportedAcceptance report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Write([.. Load().Where(existing => !Matches(existing, report.Project, report.Slug)), report]);
    }

    /// <summary>Removes a workstream's report, once confirmed or no longer true.</summary>
    public void Remove(string project, string slug) =>
        Write([.. Load().Where(existing => !Matches(existing, project, slug))]);

    private void Write(IReadOnlyList<ReportedAcceptance> reports)
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(reports, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The reported acceptances could not be written to {Path}", FilePath);
        }
    }

    private static bool Matches(ReportedAcceptance report, string project, string slug) =>
        string.Equals(report.Project, project, StringComparison.OrdinalIgnoreCase)
        && string.Equals(report.Slug, slug, StringComparison.OrdinalIgnoreCase);
}
