using System.Text;

namespace Theseus.Core.Domain;

/// <summary>
/// Turns a title into the kebab-case key that names its plan file. The key is the identity that
/// ties a card, a plan file and a sidecar together, so it has to be a safe filename and it has to
/// be unique on disk.
/// </summary>
public static class WorkstreamKey
{
    /// <summary>Derives a key from a title.</summary>
    public static string FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "workstream";
        }

        var builder = new StringBuilder(title.Length);

        foreach (var character in title.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var key = builder.ToString().Trim('-');

        return key.Length == 0 ? "workstream" : key;
    }

    /// <summary>
    /// Derives a key that is not already taken, adding a numeric suffix if it is. Two workstreams
    /// sharing a key would share a plan file, and the second would silently overwrite the first.
    /// </summary>
    public static string Unique(string? title, IReadOnlyCollection<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        var root = FromTitle(title);

        if (!taken.Contains(root, StringComparer.OrdinalIgnoreCase))
        {
            return root;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{root}-{suffix}";

            if (!taken.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return $"{root}-{Guid.NewGuid():N}";
    }
}
