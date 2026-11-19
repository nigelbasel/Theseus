using System.Windows.Threading;
using Theseus.Core.Diagnostics;
using Theseus.Core.Resources;
using Theseus.Core.Sessions;

namespace Theseus.App.ViewModels;

/// <summary>
/// What each session is using, shown on its card, and what the whole machine is doing, shown in the
/// status bar: processor, memory, I/O, running agents, Defender's share and Docker's share. Nigel asked
/// on 2026-11-06 to see this on the board, so that a slow machine can be explained without running a
/// Defender recording.
///
/// The process table is read off the UI thread every few seconds. It is one system call for the whole
/// machine, and needs no elevation.
/// </summary>
public sealed class ResourcesViewModel : ObservableObject, IDisposable
{
    /// <summary>How often to read.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private const string MachineExplanation =
        "Share of the whole machine's processors.\n" +
        "sessions: every session's Claude Code process and everything it has started.\n" +
        "Defender: Microsoft Defender's scanning engine and its Defender for Endpoint sensor. It rises when sessions open or write many files, or start many processes.\n" +
        "Docker: the WSL virtual machine that Docker's containers run in.";

    private readonly Func<IReadOnlyList<WorkstreamCardViewModel>> _cards;
    private readonly Func<string?, int?> _processFor;
    private readonly ResourceSampler _sampler = new(Environment.ProcessorCount);
    private readonly SubagentActivityReader _subagents = new(WorkflowRunReader.DefaultDirectory);
    private readonly WorkflowActivityReader _workflows = new(WorkflowRunReader.DefaultDirectory);
    private readonly DispatcherTimer _timer;
    private readonly ICoreCounters _cores = new PdhCoreCounters();
    private readonly CoreClampWatch _clamp;
    private string _machineText = string.Empty;
    private string _clampText = string.Empty;
    private string _clampTooltip = string.Empty;
    private bool _sampling;
    private bool _disposed;

    /// <summary>
    /// Creates the reading over the board's cards. <paramref name="processFor"/> gives the Claude Code
    /// process of a running session, by session id.
    /// </summary>
    /// <param name="clamp">The figures for the cores-held-back warning, from the settings file.</param>
    public ResourcesViewModel(Func<IReadOnlyList<WorkstreamCardViewModel>> cards, Func<string?, int?> processFor,
        Theseus.Core.Settings.CoreClampSettings? clamp = null)
    {
        _cards = cards;
        _processFor = processFor;
        _clamp = new CoreClampWatch(clamp ?? new Theseus.Core.Settings.CoreClampSettings());
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Interval };
        _timer.Tick += async (_, _) => await SampleAsync();
        _timer.Start();
    }

    /// <summary>The status bar's line, for example "CPU 48% · sessions 30% · Defender 6% · Docker 12%".</summary>
    public string MachineText
    {
        get => _machineText;
        private set => Set(ref _machineText, value);
    }

    /// <summary>What the status bar's figures mean.</summary>
    public string MachineTooltip => MachineExplanation;

    /// <summary>
    /// The cores-held-back warning, for example "Cores held back: 16 of 20 parked under load", or empty when there is
    /// none (parked-cores, 2026-11-19).
    /// </summary>
    public string ClampText
    {
        get => _clampText;
        private set => Set(ref _clampText, value);
    }

    /// <summary>What the warning means, what to check, and the figures behind it.</summary>
    public string ClampTooltip
    {
        get => _clampTooltip;
        private set => Set(ref _clampTooltip, value);
    }

    /// <summary>Stops reading.</summary>
    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _cores.Dispose();
    }

    private async Task SampleAsync()
    {
        if (_sampling || _disposed)
        {
            return;
        }

        _sampling = true;

        try
        {
            var cards = _cards();
            var targets = cards
                .Where(card => card.IsLive)
                .Select(card => (Card: card, SessionId: card.Workstream.LastSessionId,
                    Process: _processFor(card.Workstream.LastSessionId)))
                .ToList();

            var (reading, agents, cores) = await Task.Run(() =>
            {
                var now = DateTimeOffset.UtcNow;
                var taken = _sampler.Take(SystemProcessTable.Capture(),
                    targets.Select(target => target.Process).OfType<int>());
                var running = targets.ToDictionary(target => target.Card, target => Agents(target.SessionId, now));

                return (taken, running, _cores.Read());
            });

            if (_disposed)
            {
                return;
            }

            foreach (var card in cards)
            {
                var target = targets.FirstOrDefault(candidate => ReferenceEquals(candidate.Card, card));
                var session = target.Card is null ? null : reading.Session(target.Process);

                card.Resources = session is null ? string.Empty : ResourceText.Card(session, reading.HasRates, agents[card]);
                card.ResourcesTooltip = session is null ? string.Empty : ResourceText.Tooltip(session, reading.HasRates);
            }

            MachineText = ResourceText.Machine(reading.Machine, reading.HasRates);
            ShowClamp(cores);
        }
        catch (Exception exception)
        {
            TheseusLog.Warning("Could not read what the sessions are using: {Reason}", exception.Message);
        }
        finally
        {
            _sampling = false;
        }
    }

    private void ShowClamp(CoreSample? cores)
    {
        var state = _clamp.Observe(cores);

        if (state.Started && cores is not null)
        {
            TheseusLog.Warning("{Line}", CoreClampText.Started(cores));
        }
        else if (state.Stopped)
        {
            TheseusLog.Information("{Line}", CoreClampText.Stopped(cores));
        }

        ClampText = state.Standing && cores is not null ? CoreClampText.Status(cores) : string.Empty;
        ClampTooltip = state.Standing && cores is not null ? CoreClampText.Tooltip(cores) : string.Empty;
    }

    private int Agents(string? sessionId, DateTimeOffset now)
    {
        var workflow = _workflows.Read(sessionId) is { } activity && activity.IsRunning(now)
            ? activity.Started - activity.Finished
            : 0;

        return workflow + _subagents.Running(sessionId, now);
    }
}
