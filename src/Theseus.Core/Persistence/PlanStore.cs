using System.Text.Json;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;

namespace Theseus.Core.Persistence;

/// <inheritdoc />
public sealed class PlanStore : IPlanStore
{
    private readonly string _directory;
    private readonly int _attempts;
    private readonly TimeSpan _retryDelay;

    /// <summary>Creates a store over a directory of plan files.</summary>
    /// <param name="directory">The directory holding <c>&lt;key&gt;.json</c> files.</param>
    /// <param name="attempts">How many times to re-read a file that will not parse.</param>
    /// <param name="retryDelay">How long to wait between attempts.</param>
    public PlanStore(string directory, int attempts = 3, TimeSpan? retryDelay = null)
    {
        _directory = directory;
        _attempts = Math.Max(1, attempts);
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(250);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Keys()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(key => !string.IsNullOrEmpty(key))
            .Select(key => key!)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public string PathFor(string key) => Path.Combine(_directory, $"{key}.json");

    /// <inheritdoc />
    public SessionPlan? TryRead(string key)
    {
        var path = PathFor(key);

        for (var attempt = 0; attempt < _attempts; attempt++)
        {
            if (attempt > 0 && _retryDelay > TimeSpan.Zero)
            {
                Thread.Sleep(_retryDelay);
            }

            var text = AtomicFile.TryReadAllText(path);
            if (text is null)
            {
                continue;
            }

            SessionPlan? plan;
            try
            {
                plan = JsonSerializer.Deserialize<SessionPlan>(text, TheseusJson.Readable);
            }
            catch (JsonException exception)
            {
                TheseusLog.Warning("Plan {Key} did not parse on attempt {Attempt}: {Reason}", key, attempt + 1, exception.Message);

                continue;
            }

            if (plan is not null && IsUsable(plan))
            {
                return plan;
            }

            if (plan is not null)
            {
                TheseusLog.Warning("Plan {Key} is missing a prompt, working directory or timestamp, so it is being ignored", key);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Write(string key, SessionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        AtomicFile.WriteAllText(PathFor(key), JsonSerializer.Serialize(plan, TheseusJson.Readable));
    }

    /// <inheritdoc />
    public void Delete(string key)
    {
        try
        {
            File.Delete(PathFor(key));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Plan {Key} could not be deleted", key);
        }
    }

    private static bool IsUsable(SessionPlan plan) =>
        !string.IsNullOrWhiteSpace(plan.Prompt)
        && !string.IsNullOrWhiteSpace(plan.Cwd)
        && !string.IsNullOrWhiteSpace(plan.WrittenAt);
}
