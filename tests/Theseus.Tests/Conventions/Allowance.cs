using System.Globalization;

namespace Theseus.Tests.Conventions;

/// <summary>
/// One line of AllowList.txt: <c>Rule | value | scope | reason</c>. The scope is <c>*</c> for every file, <c>glyph</c> for
/// a glyph anywhere, or a path under src/Theseus.App. The value <c>nudge</c> matches spacing whose only off-grid parts are
/// a top or bottom of 1 or 2, the text-baseline nudge baseline W36 allows.
/// </summary>
public sealed record Allowance(string Rule, string Value, string Scope, string Reason)
{
    /// <summary>Reads a line of the allow-list.</summary>
    public static Allowance Parse(string line)
    {
        string[] parts = line.Split('|', 4, StringSplitOptions.TrimEntries);

        return parts.Length == 4
            ? new Allowance(parts[0], parts[1], parts[2], parts[3])
            : throw new FormatException($"AllowList.txt: '{line}' is not 'Rule | value | scope | reason'");
    }

    /// <summary>Whether this allowance keeps the given finding in the given file.</summary>
    public bool Allows(string rule, string value, string file, bool glyph)
    {
        bool inScope = Scope switch
        {
            "*" => true,
            "glyph" => glyph,
            _ => string.Equals(Scope, file, StringComparison.Ordinal),
        };

        return inScope && rule == Rule && (Value == "nudge" ? rule == "Spacing" && IsNudge(value) : value == Value);
    }

    private static bool IsNudge(string value)
    {
        double[] parts = [.. value.Split(',').Select(part => double.Parse(part, CultureInfo.InvariantCulture))];
        double[] sides = parts.Length switch
        {
            1 => [parts[0], parts[0], parts[0], parts[0]],
            2 => [parts[0], parts[1], parts[0], parts[1]],
            _ => parts,
        };

        return sides[0] % 4 == 0 && sides[2] % 4 == 0 && OnGridOrNudge(sides[1]) && OnGridOrNudge(sides[3]);
    }

    private static bool OnGridOrNudge(double side) => side % 4 == 0 || Math.Abs(side) is 1 or 2;
}
