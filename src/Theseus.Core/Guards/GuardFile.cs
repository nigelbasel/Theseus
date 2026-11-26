using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Theseus.Core.Guards;

/// <summary>
/// A file a guard installs, as this Theseus was built with it: embedded in the build, so the installed copy is always the
/// one this board agrees with. Its text has LF line endings whatever the checkout's, because one of them is a bash script.
/// </summary>
public sealed class GuardFile
{
    private readonly Lazy<string> _text;

    /// <summary>A file embedded in this build.</summary>
    /// <param name="fileName">Its name where it is installed, as hook commands name it.</param>
    /// <param name="resource">Its manifest resource name.</param>
    public GuardFile(string fileName, string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        FileName = fileName;
        _text = new Lazy<string>(() => Read(resource));
    }

    /// <summary>Its name where it is installed.</summary>
    public string FileName { get; }

    /// <summary>Its text, with LF line endings.</summary>
    public string Text => _text.Value;

    /// <summary>A short name for its content, the same whatever its line endings.</summary>
    public string Version => VersionOf(Text);

    /// <summary>A short name for a file's content, the same whatever its line endings.</summary>
    /// <param name="text">The file's text.</param>
    public static string VersionOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))))[..12];
    }

    private static string Read(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"{resource} is not in this build");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
