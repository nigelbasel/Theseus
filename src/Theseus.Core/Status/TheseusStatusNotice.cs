namespace Theseus.Core.Status;

/// <summary>
/// What a session is told about Theseus stopping and starting (Nigel, 2026-10-30: sessions should know
/// it has stopped "so they dont get confused about the inability to get a lock or check times").
/// </summary>
public static class TheseusStatusNotice
{
    /// <summary>
    /// Why Theseus is not running, for a session whose Theseus tool call would otherwise fail with a
    /// bare connection error, or null when it is running.
    /// </summary>
    public static string? WhyNotRunning(TheseusStatus? status, Func<int, bool> isRunning, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(isRunning);

        if (status is null)
        {
            return "Theseus is not running on this machine";
        }

        if (status.StoppedUtc is { } stopped)
        {
            return $"Theseus is not running: it was closed at {Local(stopped, zone)}";
        }

        return isRunning(status.ProcessId)
            ? null
            : $"Theseus is not running: it stopped unexpectedly, having started at {Local(status.StartedUtc, zone)}";
    }

    /// <summary>The reply to a Theseus tool call made while Theseus is down.</summary>
    public static string ToolRefusal(string whyNotRunning) =>
        $"{whyNotRunning}. Its lock, time, link and proposal tools are unavailable until the user starts it again. "
        + "Do not treat a lock as free in the meantime. Wait, or tell the user and ask them to start Theseus.";

    /// <summary>
    /// A short mark of which Theseus a session last saw and in what state, so a change is told once.
    /// </summary>
    public static string Marker(TheseusStatus? status, Func<int, bool> isRunning)
    {
        ArgumentNullException.ThrowIfNull(isRunning);

        if (status is null)
        {
            return "none";
        }

        var state = status.StoppedUtc is not null ? "stopped" : isRunning(status.ProcessId) ? "running" : "crashed";

        return $"{status.StartedUtc:O}|{state}";
    }

    /// <summary>
    /// What to tell a session whose last sight of Theseus was <paramref name="seen"/>, or null when
    /// nothing has changed or it has never looked.
    /// </summary>
    public static string? ChangeSince(string? seen, TheseusStatus? status, Func<int, bool> isRunning, TimeZoneInfo zone)
    {
        if (seen is null || seen == Marker(status, isRunning))
        {
            return null;
        }

        if (WhyNotRunning(status, isRunning, zone) is { } why)
        {
            return $"{why}. Its lock, time, link and proposal tools will fail until it is back; wait, or ask the user to start it.";
        }

        return $"Theseus restarted at {Local(status!.StartedUtc, zone)}. Any lock this session held or was queued for has gone; "
               + "ask for it again. If a Theseus tool fails to connect, ask the user to run /mcp in this tab to reconnect it.";
    }

    private static string Local(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
