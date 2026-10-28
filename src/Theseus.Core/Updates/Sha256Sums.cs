using System.Security.Cryptography;

namespace Theseus.Core.Updates;

/// <summary>Reads a SHA256SUMS.txt ("&lt;hash&gt;  &lt;file name&gt;" per line) and checks bytes against it.</summary>
public static class Sha256Sums
{
    /// <summary>The lowercase hash listed for a file, or null when the file is not listed.</summary>
    public static string? HashFor(string sums, string fileName)
    {
        ArgumentNullException.ThrowIfNull(sums);

        foreach (var line in sums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 2 && string.Equals(parts[1].TrimStart('*'), fileName, StringComparison.OrdinalIgnoreCase)
                && parts[0].Length == 64)
            {
                return parts[0].ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>Whether these bytes hash to the expected lowercase hex SHA-256.</summary>
    public static bool Matches(byte[] bytes, string expected) =>
        string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), expected, StringComparison.OrdinalIgnoreCase);
}
