using Theseus.Core.Domain;

namespace Theseus.Core.Attention;

/// <summary>
/// Every session the run log knows, with the card it ran for (7.3), so a busy session counts as machine time on its card
/// whether or not it is the card's last. The run log is read again only when its length changes: the sampler asks every
/// five seconds and the log runs to megabytes.
/// </summary>
public sealed class SessionCards
{
    private readonly Func<IReadOnlyList<SessionRun>> _read;
    private readonly Func<long> _length;
    private readonly Lock _gate = new();
    private long _readAt = -1;
    private IReadOnlyDictionary<string, string> _cards = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the index.</summary>
    /// <param name="read">Reads the run log.</param>
    /// <param name="length">The run log's length now, or -1 when it cannot be had; a change means read again.</param>
    public SessionCards(Func<IReadOnlyList<SessionRun>> read, Func<long> length)
    {
        _read = read;
        _length = length;
    }

    /// <summary>Over a file: its length is read from the file system.</summary>
    public static SessionCards Over(string path, Func<IReadOnlyList<SessionRun>> read) =>
        new(read, () =>
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return -1;
            }
        });

    /// <summary>Session id to card key, the latest run of each session winning.</summary>
    public IReadOnlyDictionary<string, string> Current()
    {
        var length = _length();

        lock (_gate)
        {
            if (length != _readAt || length < 0)
            {
                var cards = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var run in _read().OrderBy(run => run.StartedUtc))
                {
                    if (!string.IsNullOrWhiteSpace(run.ClaudeSessionId) && !string.IsNullOrWhiteSpace(run.WorkstreamKey))
                    {
                        cards[run.ClaudeSessionId] = run.WorkstreamKey;
                    }
                }

                _cards = cards;
                _readAt = length;
            }

            return _cards;
        }
    }
}
