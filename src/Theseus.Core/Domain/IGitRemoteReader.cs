namespace Theseus.Core.Domain;

/// <summary>Reads the origin remote of the repository a directory belongs to.</summary>
public interface IGitRemoteReader
{
    /// <summary>The origin url, or null when the directory is not in a repository.</summary>
    string? ReadOriginUrl(string? directory);
}
