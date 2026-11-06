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
        "Defender: Microsoft Defender's scanning engine. It rises when sessions open or write many files.\n" +
        "Docker: the WSL virtual machine that Docker's containers run in.";

    private readonly Func<IReadOnlyList<WorkstreamCardViewModel>> _cards;
    private readonly Func<string?, int?> _processFor;
    private readonly ResourceSampler _sampler = new(Environment.ProcessorCount);
    private readonly SubagentActivityReader _subagents = new(WorkflowRunReader.DefaultDirectory);
    private readonly WorkflowActivityReader _workflows = new(WorkflowRunReader.DefaultDirectory);
    private readonly DispatcherTimer _timer;
    private string _machineText = string.Empty;
    private bool _sampling;
    private bool _disposed;

    /// <summary>
    /// Creates the reading over the board's cards. <paramref name="processFor"/> gives the Claude Code
    /// process of a running session, by session id.
    /// </summary>
    public ResourcesViewModel(Func<IReadOnlyList<WorkstreamCardViewModel>> cards, Func<string?, int?> processFor)
    {
        _cards = cards;
        _processFor = processFor;
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

    /// <summary>Stops reading.</summary>
    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
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

            var (reading, agents) = await Task.Run(() =>
            {
                var now = DateTimeOffset.UtcNow;
                var taken = _sampler.Take(SystemProcessTable.Capture(),
                    targets.Select(target => target.Process).OfType<int>());
                var running = targets.ToDictionary(target => target.Card, target => Agents(target.SessionId, now));

                return (taken, running);
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

    private int Agents(string? sessionId, DateTimeOffset now)
    {
        var workflow = _workflows.Read(sessionId) is { } activity && activity.IsRunning(now)
            ? activity.Started - activity.Finished
            : 0;

        return workflow + _subagents.Running(sessionId, now);
    }
}
