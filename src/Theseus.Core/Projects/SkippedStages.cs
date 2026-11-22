using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// The stages a session said it skipped, and why: lines of a piece's <c>skipped.md</c>, <c>&lt;stage&gt;: &lt;reason&gt;</c>
/// (1.1f E30a). A stage is named as the view names it (Reproduce, Rule, Fix for a bug) or as the pipeline does.
/// </summary>
public static partial class SkippedStages
{
    /// <summary>The file in a piece's records folder that lists its skipped stages.</summary>
    public const string FileName = "skipped.md";

    /// <summary>Each stage named in the text, with its reason; a line that names no stage is prose and is left alone.</summary>
    public static IReadOnlyDictionary<PipelineStage, string> Read(string? text)
    {
        var skipped = new Dictionary<PipelineStage, string>();

        foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (SkipLine().Match(line.Trim()) is { Success: true } match && StageNamed(match.Groups[1].Value) is { } stage
                && match.Groups[2].Value.Trim() is { Length: > 0 } reason)
            {
                skipped[stage] = reason;
            }
        }

        return skipped;
    }

    /// <summary>The stage a word names, or null.</summary>
    public static PipelineStage? StageNamed(string word) => word.Trim().ToLowerInvariant() switch
    {
        "research" or "reproduce" => PipelineStage.Research,
        "spec" or "rule" => PipelineStage.Spec,
        "plan" => PipelineStage.Plan,
        "implement" or "build" or "fix" => PipelineStage.Implement,
        "verify" or "check" => PipelineStage.Verify,
        "review" => PipelineStage.Review,
        "lessons" or "compound" => PipelineStage.Compound,
        _ => null,
    };

    [GeneratedRegex(@"^(?:[-*]\s+)?([A-Za-z]+)\s*:\s*(.+)$")]
    private static partial Regex SkipLine();
}
