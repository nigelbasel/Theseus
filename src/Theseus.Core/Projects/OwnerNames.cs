namespace Theseus.Core.Projects;

/// <summary>
/// The names an older <c>Waiting: owner: Who: what</c> line may call the owner by: the account's name, its full name and
/// its first name, matched without case. A line written since the <c>on &lt;who&gt;:</c> form needs none (git-22 for Nigel,
/// 2026-11-24: an account "ada" with the full name "Ada Lovelace", where a lead writes "Ada").
/// </summary>
public static class OwnerNames
{
    /// <summary>The names, from the account's name and its full name, either possibly empty.</summary>
    public static IReadOnlyList<string> From(string? account, string? fullName)
    {
        var full = fullName?.Trim() ?? string.Empty;
        var first = full.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

        return [.. new[] { account?.Trim() ?? string.Empty, full, first }.Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
