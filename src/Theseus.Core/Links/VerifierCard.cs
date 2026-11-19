using System.Globalization;
using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>
/// The card Verify with holdouts… creates for a card whose change is ready to verify: it runs in the holdout clone,
/// reaches the checked card's repos as extra directories, and reports to a dated file in the holdout's results folder,
/// where the scenarios' details stay private (verifier-card AC 10 and 11; decisions D3 and D4, Nigel 2026-11-19).
/// </summary>
/// <param name="Title">"Verify: " and the checked card's title.</param>
/// <param name="WorkingDirectory">The holdout clone.</param>
/// <param name="AddDirectories">The checked card's working directory and extra directories.</param>
/// <param name="Prompt">The fixed brief.</param>
/// <param name="ReportPath">The report document, in the holdout's results folder.</param>
public sealed record VerifierCard(
    string Title,
    string WorkingDirectory,
    IReadOnlyList<string> AddDirectories,
    string Prompt,
    string ReportPath)
{
    /// <summary>The role every verifier card has.</summary>
    public const WorkstreamRole Role = WorkstreamRole.Verifier;

    /// <summary>The folder in the holdout clone that a verifier may write to.</summary>
    public const string ResultsFolder = "results";

    /// <summary>The verifier card for a checked card.</summary>
    /// <param name="checkedCard">The card whose change is verified.</param>
    /// <param name="holdoutFolder">The holdout clone, from Settings.</param>
    /// <param name="now">The moment it is created, in the zone its report name should read in.</param>
    public static VerifierCard For(Workstream checkedCard, string holdoutFolder, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(checkedCard);
        ArgumentException.ThrowIfNullOrWhiteSpace(holdoutFolder);

        var report = Path.Combine(holdoutFolder, ResultsFolder,
            $"{checkedCard.Key}-{now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.md");

        IReadOnlyList<string> reach = [.. new[] { checkedCard.WorkingDirectory }
            .Concat(checkedCard.AddDirectories)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return new VerifierCard($"Verify: {checkedCard.Title}", holdoutFolder, reach,
            VerifierBrief.For(checkedCard.Title, report), report);
    }
}
