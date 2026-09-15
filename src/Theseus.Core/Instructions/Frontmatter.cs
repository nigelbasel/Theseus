namespace Theseus.Core.Instructions;

/// <summary>
/// The YAML block at the top of a skill, command, agent or rule, read only as far as the viewer needs:
/// top-level keys with plain values, folded text, and lists, which are joined into one string.
/// Anything cleverer is not worth a YAML parser for a list of names and descriptions.
/// </summary>
public static class Frontmatter
{
    /// <summary>The fields, or none when the text does not open with a complete block.</summary>
    public static IReadOnlyDictionary<string, string> Read(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = (text ?? string.Empty).Split('\n').Select(line => line.TrimEnd('\r')).ToList();

        if (lines.Count == 0 || lines[0].Trim() != "---")
        {
            return fields;
        }

        string? key = null;

        foreach (var line in lines.Skip(1))
        {
            if (line.Trim() == "---")
            {
                return fields;
            }

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && line.IndexOf(':') is > 0 and var colon)
            {
                key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                fields[key] = value is ">" or "|" or ">-" or "|-" ? string.Empty : Unquote(value);

                continue;
            }

            if (key is null || line.Trim().Length == 0)
            {
                continue;
            }

            var continued = line.Trim();
            var item = continued.StartsWith('-') ? Unquote(continued[1..].Trim()) : continued;
            var separator = continued.StartsWith('-') ? ", " : " ";

            fields[key] = fields[key].Length == 0 ? item : fields[key] + separator + item;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\'')
            ? value[1..^1]
            : value;
}
