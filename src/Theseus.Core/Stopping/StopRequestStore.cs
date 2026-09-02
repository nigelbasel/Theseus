using System.Text.Json;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Stopping;

/// <summary>
/// Stop requests, as files.
///
/// Claiming one is a move from the pending folder into the delivered folder, and that is deliberate:
/// a move is atomic, so exactly one caller can ever win a request however many are looking. A
/// session asked to stop must be asked once. Told twice - by two hooks, or by a session forked onto
/// the same identifier - it would write its handoff, exit, and be interrupted again on the way out.
///
/// The move is also the receipt. What was delivered, and when, is on disk without a second file to
/// keep in step with the first.
/// </summary>
public sealed class StopRequestStore : IStopRequestStore
{
    private readonly string _root;

    /// <summary>Creates the store over the directory that holds the requests.</summary>
    public StopRequestStore(string root) => _root = root;

    /// <inheritdoc />
    public void Write(PendingStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (StopRequestPath.For(_root, request.SessionId) is not { } path)
        {
            TheseusLog.Warning("A stop request was not written: {SessionId} is not a session id", request.SessionId);

            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(request, TheseusJson.Compact));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "A stop request could not be written to {Path}", path);
        }
    }

    /// <inheritdoc />
    public bool Cancel(string sessionId)
    {
        if (StopRequestPath.For(_root, sessionId) is not { } path)
        {
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "A stop request could not be withdrawn from {Path}", path);

            return false;
        }
    }

    /// <inheritdoc />
    public PendingStopRequest? Claim(string sessionId)
    {
        if (StopRequestPath.For(_root, sessionId) is not { } pending
            || StopRequestPath.For(_root, sessionId, StopRequestPath.DeliveredFolder) is not { } delivered)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(delivered)!);
            File.Move(pending, delivered, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return Parse(delivered);
    }

    /// <inheritdoc />
    public PendingStopRequest? Waiting(string sessionId) =>
        StopRequestPath.For(_root, sessionId) is { } path ? Parse(path) : null;

    /// <inheritdoc />
    public IReadOnlyList<PendingStopRequest> Pending()
    {
        var directory = Path.Combine(_root, StopRequestPath.PendingFolder);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateFiles(directory, "*.json").Select(Parse).OfType<PendingStopRequest>()];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The waiting stop requests could not be listed");

            return [];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PendingStopRequest> Handed()
    {
        var directory = Path.Combine(_root, StopRequestPath.DeliveredFolder);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateFiles(directory, "*.json").Select(Parse).OfType<PendingStopRequest>()];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The delivered stop requests could not be listed");

            return [];
        }
    }

    /// <inheritdoc />
    public PendingStopRequest? Delivered(string? sessionId) =>
        StopRequestPath.For(_root, sessionId, StopRequestPath.DeliveredFolder) is { } path
            ? Parse(path)
            : null;

    /// <inheritdoc />
    public bool Retire(string? sessionId)
    {
        if (StopRequestPath.For(_root, sessionId, StopRequestPath.DeliveredFolder) is not { } path)
        {
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "A finished stop request could not be retired from {Path}", path);

            return false;
        }
    }

    /// <inheritdoc />
    public int Sweep(DateTimeOffset now)
    {
        var gone = 0;

        foreach (var stale in Pending().Where(request => request.ExpiresUtc <= now))
        {
            if (Cancel(stale.SessionId))
            {
                gone++;
            }
        }

        // Delivered requests expire too. Sweeping only the waiting ones left them on disk
        // indefinitely - one here was five days old and still armed - so a session that was never
        // successfully told to exit stayed a target for every Theseus that started afterwards.
        foreach (var stale in Handed().Where(request => request.ExpiresUtc <= now))
        {
            if (Retire(stale.SessionId))
            {
                gone++;
            }
        }

        if (gone > 0)
        {
            TheseusLog.Debug("Discarded {Count} stop request(s) nothing had come back for", gone);
        }

        return gone;
    }

    private static PendingStopRequest? Parse(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<PendingStopRequest>(File.ReadAllText(path), TheseusJson.Compact);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
