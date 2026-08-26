using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Theseus.Core.Diagnostics;

/// <summary>
/// The application's log.
///
/// It exists because almost everything this application does is a guess about somebody else's
/// files, processes and windows, and those guesses fail in ways that leave no trace anywhere else:
/// a plan file half-written by a session, a registry entry for a pid that has been reused, a
/// terminal tab that cannot be identified. Swallowing those quietly makes the application look
/// merely inert when it is actually failing, and leaves nothing to investigate afterwards.
///
/// A failure that is expected and handled is recorded at debug; one that loses information or a
/// user action is a warning or an error. Nothing logged here ever includes a token or a credential.
/// </summary>
public static class TheseusLog
{
    private static ILogger _logger = Logger.None;

    /// <summary>Where the log files are written.</summary>
    public static string? Directory { get; private set; }

    /// <summary>Starts logging to a daily file under the given directory.</summary>
    public static void Start(string directory, LogEventLevel minimum = LogEventLevel.Debug)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimum)
            .WriteTo.File(
                Path.Combine(directory, "theseus-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _logger.Information("Theseus started. Logging to {Directory}", directory);
    }

    /// <summary>Flushes and closes the log.</summary>
    public static void Stop()
    {
        (_logger as IDisposable)?.Dispose();
        _logger = Logger.None;
    }

    /// <summary>An expected, handled failure worth having a record of.</summary>
    public static void Debug(string template, params object?[] values) =>
        _logger.Debug(template, values);

    /// <summary>Something that worked but not as intended.</summary>
    public static void Warning(string template, params object?[] values) =>
        _logger.Warning(template, values);

    /// <summary>Something that worked but not as intended, with the exception behind it.</summary>
    public static void Warning(Exception exception, string template, params object?[] values) =>
        _logger.Warning(exception, template, values);

    /// <summary>Something that failed and cost the user an action or their data.</summary>
    public static void Error(Exception exception, string template, params object?[] values) =>
        _logger.Error(exception, template, values);

    /// <summary>Something worth recording that is not a failure.</summary>
    public static void Information(string template, params object?[] values) =>
        _logger.Information(template, values);
}
