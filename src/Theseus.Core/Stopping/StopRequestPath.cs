namespace Theseus.Core.Stopping;

/// <summary>
/// Where a request for one session lives, and the only place a session identifier becomes a path.
///
/// The identifier arrives on a hook's standard input, which is to say from outside. It is validated
/// as a session identifier before it is combined with anything, so a crafted one cannot name a file
/// outside the directory it belongs in. Anything that is not one yields nothing at all, and the hook
/// stays silent rather than touching the filesystem.
/// </summary>
public static class StopRequestPath
{
    /// <summary>The folder holding requests that have not been delivered.</summary>
    public const string PendingFolder = "pending";

    /// <summary>The folder a request is moved into once a session has been given it.</summary>
    public const string DeliveredFolder = "delivered";

    /// <summary>The file holding the request for one session, or null when the identifier is not one.</summary>
    public static string? For(string root, string? sessionId, string folder = PendingFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return Guid.TryParseExact(sessionId, "D", out var id)
            ? Path.Combine(root, folder, $"{id:D}.json")
            : null;
    }
}
