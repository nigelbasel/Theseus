using System.Windows.Threading;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Usage;

namespace Theseus.App.ViewModels;

/// <summary>
/// The account's usage, in the status bar. The endpoint behind this refuses far more readily than it
/// serves, so the timer ticks often but a read only happens when the schedule allows one; a refusal
/// backs off rather than being retried.
/// </summary>
public sealed class UsageViewModel : ObservableObject, IDisposable
{
    private readonly IUsageClient _client;
    private readonly UsagePollSchedule _schedule;
    private readonly ISystemClock _clock;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _stopping = new();

    private AccountUsage _usage = AccountUsage.None;
    private string _problem = string.Empty;
    private bool _isReading;

    /// <summary>Creates the status bar's usage reading and begins polling.</summary>
    public UsageViewModel(IUsageClient client, UsagePollSchedule schedule, ISystemClock clock)
    {
        _client = client;
        _schedule = schedule;
        _clock = clock;

        RefreshCommand = new RelayCommand(_ => _ = PollAsync(), _ => _schedule.MayPollNow && !_isReading);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => _ = PollAsync();
        _timer.Start();

        _ = PollAsync();
    }

    /// <summary>Reads usage now, if the schedule allows it.</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>The five-hour window, or empty while there is no reading.</summary>
    public string SessionText => Describe(_usage.Session);

    /// <summary>The seven-day window.</summary>
    public string WeeklyText => Describe(_usage.Weekly);

    /// <summary>
    /// The window actually constraining the account, shown only when it is neither of the two
    /// headline windows — a model-scoped weekly limit can bind while both of those look comfortable.
    /// </summary>
    public string BindingText
    {
        get
        {
            var binding = _usage.Binding;

            if (binding is null || binding.Kind is "session" or "weekly_all")
            {
                return string.Empty;
            }

            return Describe(binding);
        }
    }

    /// <summary>Whether the binding window is one the service has flagged.</summary>
    public bool IsWarning => _usage.Binding?.Severity is "warning" or "critical";

    /// <summary>Whether there is anything to show.</summary>
    public bool HasReading => _usage.HasReading;

    /// <summary>What went wrong, when nothing can be shown.</summary>
    public string Problem
    {
        get => _problem;
        private set => Set(ref _problem, value);
    }

    /// <summary>How old the reading is, so a stale one is visibly stale.</summary>
    public string Age
    {
        get
        {
            if (!_usage.HasReading)
            {
                return string.Empty;
            }

            var elapsed = _clock.UtcNow - _usage.RetrievedUtc;

            return elapsed < TimeSpan.FromMinutes(1) ? "just now" : $"{(int)elapsed.TotalMinutes}m ago";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Stop();
        _stopping.Cancel();
        _stopping.Dispose();
    }

    private async Task PollAsync()
    {
        if (_isReading || !_schedule.MayPollNow || _stopping.IsCancellationRequested)
        {
            RaiseAll();

            return;
        }

        _isReading = true;

        try
        {
            var result = await _client.FetchAsync(_stopping.Token);

            if (result.WasThrottled)
            {
                _schedule.RecordThrottled();
                Problem = "usage rate limited";
            }
            else if (result.Usage.HasReading)
            {
                _schedule.RecordSuccess();
                _usage = result.Usage;
                Problem = string.Empty;
            }
            else
            {
                _schedule.RecordFailure();
                Problem = result.Problem ?? "usage unavailable";
            }
        }
        catch (OperationCanceledException)
        {
            TheseusLog.Debug("The usage read was abandoned because Theseus is closing");
        }
        finally
        {
            _isReading = false;
            RaiseAll();
        }
    }

    private string Describe(UsageLimit? limit) =>
        limit is null ? string.Empty : UsageFormatter.Describe(limit, _clock.UtcNow);

    private void RaiseAll()
    {
        Raise(nameof(SessionText));
        Raise(nameof(WeeklyText));
        Raise(nameof(BindingText));
        Raise(nameof(IsWarning));
        Raise(nameof(HasReading));
        Raise(nameof(Age));
    }
}
