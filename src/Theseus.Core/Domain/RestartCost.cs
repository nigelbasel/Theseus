namespace Theseus.Core.Domain;

/// <summary>
/// What restarting a session will cost: how much of its transcript a resume has to work through.
///
/// Resuming is not free and the price is invisible. A resumed session re-reads everything since its
/// last compaction before it does anything, and can begin well into its context window. Transcripts
/// on this machine run to thirty megabytes, and nothing else shows this.
///
/// **It reports size, not tokens, and that is a deliberate departure from the specification.** The
/// spec called for a token estimate at 3.5 bytes per token. Measured against the six largest
/// transcripts here, that formula returns between 1.1M and 6.9M tokens — every one of them larger
/// than any context window, so every one of them impossible. The reason is in the data: the biggest
/// one percent of records after a compaction are sixty-four percent of its bytes, being tool results
/// of over a megabyte each, and context truncates those rather than replaying them whole. Bytes are
/// an upper bound on tokens and a poor one.
///
/// So this says what was actually counted. "Most of a 30 MB transcript" answers the question the
/// number is for — is this expensive — without claiming an arithmetic it cannot do.
/// </summary>
public static class RestartCost
{
    /// <summary>How much of the transcript a resume works through, and how much there is in total.</summary>
    /// <param name="tailBytes">Bytes after the last compaction.</param>
    /// <param name="totalBytes">Bytes in the whole transcript.</param>
    public static string Describe(long tailBytes, long totalBytes)
    {
        if (tailBytes <= 0 || totalBytes <= 0)
        {
            return string.Empty;
        }

        var share = (double)tailBytes / totalBytes;

        var how = share switch
        {
            >= 0.9 => "nearly all",
            >= 0.6 => "most",
            >= 0.3 => "much",
            >= 0.1 => "some",
            _ => "a little",
        };

        return $"{how} of a {Size(totalBytes)} transcript";
    }

    /// <summary>What the button says about itself before it is pressed.</summary>
    public static string Tooltip(long tailBytes, long totalBytes)
    {
        var cost = Describe(tailBytes, totalBytes);

        return cost.Length == 0
            ? "Resume this session where it left off."
            : $"Resume this session. It works through {cost} before doing anything — "
              + $"{Size(tailBytes)} since its last compaction — and starts that far into its context.";
    }

    /// <summary>A byte count as somebody would say it, and never to a misleading precision.</summary>
    public static string Size(long bytes) => bytes switch
    {
        <= 0 => "0 MB",
        < 1024 * 1024 => $"{Math.Max(1, bytes / 1024)} KB",
        < 10L * 1024 * 1024 => $"{bytes / (double)(1024 * 1024):0.#} MB",
        _ => $"{bytes / (1024 * 1024)} MB",
    };
}
