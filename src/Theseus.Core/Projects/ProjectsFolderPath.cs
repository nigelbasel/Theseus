namespace Theseus.Core.Projects;

/// <summary>
/// A projects folder as typed, made the path Windows means by it. Windows drops a trailing dot or space when it opens a
/// path, so <c>E:\projects.</c> reads as <c>E:\projects</c>, but a file watcher given the typed form fails on it; a path
/// pasted from a sentence often carries its full stop.
/// </summary>
public static class ProjectsFolderPath
{
    /// <summary>The folder, trimmed of spaces and trailing dots and made full; empty for no folder.</summary>
    public static string Normalize(string? typed)
    {
        var trimmed = (typed ?? string.Empty).Trim().TrimEnd('.', ' ');

        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return trimmed;
        }
    }
}
