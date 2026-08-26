namespace Theseus.Core.Sessions;

/// <summary>Names a session briefly enough for a menu.</summary>
public static class SessionLabel
{
    private const int Longest = 40;

    /// <summary>
    /// The session's own name, the folder it is working in, or its identifier, in that order. A
    /// session that was never named still has to be tellable apart from the others in a list.
    /// </summary>
    public static string For(LiveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return Shortened(session.Name)
               ?? Shortened(LeafOf(session.Cwd))
               ?? session.SessionId[..Math.Min(8, session.SessionId.Length)];
    }

    private static string? LeafOf(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        return Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string? Shortened(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= Longest ? trimmed : trimmed[..(Longest - 1)] + "…";
    }
}
