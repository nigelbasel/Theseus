using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// Finds the current result block in an evidence.md: the last fenced block tagged <c>theseus-evidence</c>, holding JSON
/// (method/project.md, from the evidence-block proposal as answered on 2026-11-20).
/// </summary>
public static partial class EvidenceBlockReader
{
    /// <summary>The last block, or null when the file has none.</summary>
    public static EvidenceBlock? Last(string? evidenceText)
    {
        if (evidenceText is null)
        {
            return null;
        }

        var blocks = Block().Matches(evidenceText.Replace("\r\n", "\n", StringComparison.Ordinal));

        if (blocks.Count == 0)
        {
            return null;
        }

        var json = blocks[^1].Groups["json"].Value;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject block)
            {
                return new EvidenceBlock(null, "the last theseus-evidence block is not a JSON object", []);
            }

            var gates = block["gates"] is JsonArray claims
                ? claims.OfType<JsonObject>()
                    .Select(claim => new EvidenceGateClaim(Text(claim["gate"]) ?? string.Empty, Text(claim["accepted"])))
                    .ToList()
                : [];

            return Text(block["result"]) is { } result
                ? new EvidenceBlock(result.Trim().ToLowerInvariant(), null, gates)
                : new EvidenceBlock(null, "the last theseus-evidence block has no result", gates);
        }
        catch (JsonException exception)
        {
            return new EvidenceBlock(null, $"the last theseus-evidence block is not valid JSON: {exception.Message}", []);
        }
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    [GeneratedRegex(@"^[ \t]*```[ \t]*theseus-evidence[ \t]*\n(?<json>.*?)^[ \t]*```[ \t]*$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Block();
}
