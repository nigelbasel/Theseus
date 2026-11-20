using System.Security.Cryptography;
using System.Text;

namespace Theseus.Core.Projects;

/// <summary>
/// What a spec's acceptance is compared by: its content, not its commit, so merging the workstream's branch into the
/// primary (a new commit, the same spec) does not undo an acceptance, and any change to the text does (spec AC 7).
/// </summary>
public static class SpecIdentity
{
    /// <summary>The identity of a spec's text, line endings aside.</summary>
    public static string Hash(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normal = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normal)));
    }
}
