using System.Security.Cryptography;
using System.Text;

namespace Theseus.Core.Projects;

/// <summary>
/// What a spec's acceptance is compared by: its content, so any change to the text undoes an acceptance and nothing
/// else does (spec AC 7, 1.1b AC 5).
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
