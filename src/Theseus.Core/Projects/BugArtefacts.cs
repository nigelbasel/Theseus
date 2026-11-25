using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// A bug's stages from its <c>fix.md</c> (spec AC 6): a <c>## Spec</c> heading is the rule, a <c>## Cause</c> or
/// <c>## Change</c> heading is the fix. Headings may carry more words after their first, as the stage 0 files do.
/// </summary>
public static partial class BugArtefacts
{
    /// <summary>Whether fix.md has its rule written.</summary>
    public static bool HasRule(string? fixText) => fixText is not null && SpecHeading().IsMatch(fixText);

    /// <summary>Whether fix.md has its fix written.</summary>
    public static bool HasFix(string? fixText) => fixText is not null && FixHeading().IsMatch(fixText);

    /// <summary>The rule: the Spec section, from its heading to the next <c>## </c> heading, or null.</summary>
    public static string? RuleText(string? fixText)
    {
        if (fixText is null || SpecHeading().Match(fixText) is not { Success: true } heading)
        {
            return null;
        }

        var rest = fixText[heading.Index..];
        var next = NextHeading().Match(rest, heading.Length);

        return next.Success ? rest[..next.Index] : rest;
    }

    [GeneratedRegex(@"^##[ \t]+Spec\b.*$", RegexOptions.Multiline)]
    private static partial Regex SpecHeading();

    [GeneratedRegex(@"^##[ \t]+(Cause|Change)\b", RegexOptions.Multiline)]
    private static partial Regex FixHeading();

    [GeneratedRegex(@"^## ", RegexOptions.Multiline)]
    private static partial Regex NextHeading();
}
