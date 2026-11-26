using System.Security.Cryptography;
using System.Text;

namespace Theseus.Core.Locks;

/// <summary>
/// The lock client, its guard and the command classes they share, as this build carries them, so what Options
/// installs is what this build's lock service agrees with.
/// </summary>
public static class LockToolsBundle
{
    public static IReadOnlyList<string> FileNames { get; } = ["ndc-test-lock.sh", "ndc-test-lock-guard.js", "command-classes.json"];

    public static string Text(string fileName)
    {
        using Stream stream = typeof(LockToolsBundle).Assembly.GetManifestResourceStream($"Theseus.Core.Locks.{fileName}")
                              ?? throw new InvalidOperationException($"{fileName} is not in this build.");
        using StreamReader reader = new(stream);

        return reader.ReadToEnd();
    }

    public static string VersionOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))))[..12];
    }
}
