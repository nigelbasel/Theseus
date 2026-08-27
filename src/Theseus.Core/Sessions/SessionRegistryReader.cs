using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <inheritdoc />
public sealed class SessionRegistryReader : ISessionRegistryReader
{
    private static readonly JsonSerializerOptions SerialiserOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _directory;
    private readonly IProcessProbe _probe;

    /// <summary>Creates a reader over a session registry directory.</summary>
    public SessionRegistryReader(string directory, IProcessProbe probe)
    {
        _directory = directory;
        _probe = probe;
    }

    /// <summary>The registry directory Claude Code writes to for the current user.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "sessions");

    /// <inheritdoc />
    public SessionRegistrySnapshot Read()
    {
        if (!Directory.Exists(_directory))
        {
            return SessionRegistrySnapshot.Empty;
        }

        var live = new List<LiveSession>();
        var corpses = 0;
        var unreadable = 0;

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            var entry = TryRead(file);
            if (entry is null)
            {
                unreadable++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.SessionId) || entry.Pid <= 0)
            {
                unreadable++;
                continue;
            }

            if (!IsAlive(entry))
            {
                corpses++;
                continue;
            }

            live.Add(new LiveSession(
                entry.Pid,
                entry.SessionId,
                entry.Cwd,
                entry.Name,
                entry.Status,
                entry.Entrypoint,
                DateTimeOffset.FromUnixTimeMilliseconds(entry.StartedAt),
                Moment(entry.UpdatedAt),
                Moment(entry.StatusUpdatedAt)));
        }

        return new SessionRegistrySnapshot(live, corpses, unreadable);
    }

    /// <summary>
    /// A timestamp an older Claude Code may not have written. Zero means absent rather than 1970, and
    /// a caller must be able to tell "not recorded" from a real moment.
    /// </summary>
    private static DateTimeOffset? Moment(long milliseconds) =>
        milliseconds > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds) : null;

    private bool IsAlive(SessionRegistryEntry entry)
    {
        var actual = _probe.GetStartFileTime(entry.Pid);

        return actual is not null
            && !string.IsNullOrWhiteSpace(entry.ProcStart)
            && string.Equals(actual, entry.ProcStart, StringComparison.Ordinal);
    }

    private static SessionRegistryEntry? TryRead(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<SessionRegistryEntry>(File.ReadAllText(file), SerialiserOptions);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning("Session registry entry {Path} did not parse: {Reason}", file, exception.Message);

            return null;
        }
        catch (IOException exception)
        {
            TheseusLog.Debug("Session registry entry {Path} was busy: {Reason}", file, exception.Message);

            return null;
        }
    }
}
