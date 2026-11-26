using System.Globalization;
using System.Text.Json.Nodes;

namespace Theseus.Core.Locks;

/// <summary>
/// Reads a scope as clients write it: <c>name</c> or <c>name=amount</c>, comma-separated on the command line, or a
/// JSON array of those strings or of <c>{resource, amount}</c> objects.
/// </summary>
public static class ScopeText
{
    public static IReadOnlyList<ResourceClaim> Parse(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Claim)];

    public static IReadOnlyList<ResourceClaim> Parse(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out string? text))
        {
            return Parse(text);
        }

        if (node is not JsonArray array)
        {
            return [];
        }

        List<ResourceClaim> claims = [];

        foreach (JsonNode? entry in array)
        {
            if (entry is JsonValue single && single.TryGetValue<string>(out string? name) && !string.IsNullOrWhiteSpace(name))
            {
                claims.Add(Claim(name));
            }
            else if (entry is JsonObject claim && claim["resource"] is JsonValue resource && resource.TryGetValue<string>(out string? resourceName))
            {
                int amount = claim["amount"] is JsonValue count && count.TryGetValue<int>(out int parsed) ? parsed : 1;
                claims.Add(new ResourceClaim(resourceName, amount));
            }
        }

        return claims;
    }

    private static ResourceClaim Claim(string text)
    {
        int equals = text.LastIndexOf('=');

        if (equals > 0 && string.Equals(text[(equals + 1)..].Trim(), "all", StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceClaim(text[..equals].Trim(), ResourceClaim.All);
        }

        return equals > 0 && int.TryParse(text[(equals + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount)
            ? new ResourceClaim(text[..equals].Trim(), amount)
            : new ResourceClaim(text.Trim(), 1);
    }
}
