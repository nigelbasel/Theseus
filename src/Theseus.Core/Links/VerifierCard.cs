using System.Globalization;
using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>
/// The card Verify with holdouts… creates for a card whose change is ready to verify: it runs in the checked card's
/// folder, reaches the holdout clone and the checked card's other repos as extra directories, and reports to a dated
/// file in the holdout's results folder, where the scenarios' details stay private (verifier-card AC 10 and 11;
/// decisions D3 and D4, Nigel 2026-11-19; directories swapped in fix pass 2).
/// </summary>
/// <param name="Title">"Verify: " and the checked card's title.</param>
/// <param name="WorkingDirectory">The checked card's working directory.</param>
/// <param name="AddDirectories">The holdout clone, then the checked card's other extra directories.</param>
/// <param name="Prompt">The fixed brief.</param>
/// <param name="ReportPath">The report document, in the holdout's results folder.</param>
/// <param name="HandoffPath">
/// Its handoff, also in the results folder: the stop that confirming its report starts asks for a handoff, and the
/// guard refuses a verifier's writes anywhere else, so without this it could never close (verifier-card review M1).
/// </param>
public sealed record VerifierCard(
    string Title,
    string WorkingDirectory,
    IReadOnlyList<string> AddDirectories,
    string Prompt,
    string ReportPath,
    string HandoffPath)
{
    /// <summary>The role every verifier card has.</summary>
    public const WorkstreamRole Role = WorkstreamRole.Verifier;

    /// <summary>The folder in the holdout clone that a verifier may write to.</summary>
    public const string ResultsFolder = "results";

    /// <summary>The card Verify with holdouts… placed, made this verifier: its role, and its handoff in the results.</summary>
    /// <param name="created">The card as placed on the board.</param>
    public Workstream Apply(Workstream created)
    {
        ArgumentNullException.ThrowIfNull(created);

        return created with { Role = Role, StatusDocumentPath = HandoffPath };
    }

    /// <summary>The verifier card for a checked card.</summary>
    /// <param name="checkedCard">The card whose change is verified.</param>
    /// <param name="holdoutFolder">The holdout clone, from Settings.</param>
    /// <param name="now">The moment it is created, in the zone its report name should read in.</param>
    /// <param name="recordsFolder">
    /// The checked workstream's records folder in its project home, when it has one: reached as an extra directory and
    /// named in the brief, since the spec is no longer in the checked card's repos (1.1b AC 9).
    /// </param>
    public static VerifierCard For(Workstream checkedCard, string holdoutFolder, DateTimeOffset now, string? recordsFolder = null)
    {
        ArgumentNullException.ThrowIfNull(checkedCard);
        ArgumentException.ThrowIfNullOrWhiteSpace(holdoutFolder);

        var stem = Path.Combine(holdoutFolder, ResultsFolder,
            $"{checkedCard.Key}-{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}");
        var report = stem + ".md";

        // It starts in the checked card's own folder, with the holdout as an extra directory: the holdout guard refuses
        // any shell command run inside the holdout, so its builds and tests run in the code, and it reads the scenarios
        // by path (verifier-card fix pass 2, overnight decision 3, 2026-11-19).
        IReadOnlyList<string> reach = [.. new[] { holdoutFolder }
            .Concat(checkedCard.AddDirectories)
            .Concat(recordsFolder is null ? [] : [recordsFolder])
            .Where(directory => !string.Equals(directory, checkedCard.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return new VerifierCard($"Verify: {checkedCard.Title}", checkedCard.WorkingDirectory, reach,
            VerifierBrief.For(checkedCard.Title, report, recordsFolder), report, stem + "-handoff.md");
    }
}
