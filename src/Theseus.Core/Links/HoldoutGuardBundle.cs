using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Theseus.Core.Links;

/// <summary>
/// The holdout guard this Theseus was built with, from <c>tools/holdout/holdout-guard.js</c>: what Install the holdout
/// guard copies into place, so the installed guard is always the one this build's board agrees with.
/// </summary>
public static class HoldoutGuardBundle
{
    private const string Resource = "Theseus.Core.Holdout.holdout-guard.js";

    /// <summary>The guard's text.</summary>
    public static string Text { get; } = Read();

    /// <summary>A short name for this guard's content, the same whatever its line endings.</summary>
    public static string Version { get; } = VersionOf(Text);

    /// <summary>A short name for a guard's content, the same whatever its line endings.</summary>
    /// <param name="text">The guard's text.</param>
    public static string VersionOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))))[..12];
    }

    private static string Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"The holdout guard {Resource} is not in this build");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
