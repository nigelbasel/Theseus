using System.Text;
using Theseus.Core.Abstractions;
using Theseus.Core.Breakers;
using Theseus.Core.Budgets;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Settings;
using Theseus.Core.Terminal;

namespace Theseus.Core.Running;

/// <summary>
/// Runs cards headless: checks a card's breaker and budgets before it starts, holds the machine-wide cap and the queue
/// behind it, keeps every line a run writes, records each ended run in the ledger, and judges the breakers after it
/// (Nigel, 2026-11-24). It starts only what it is handed; nothing here decides that a card should run.
/// </summary>
public sealed class HeadlessRunner
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IRunLedger _ledger;
    private readonly Func<TheseusSettings> _settings;
    private readonly ISystemClock _clock;
    private readonly Func<ProcessStartRequest, Action<string>, Action<int>, IHeadlessProcess> _start;
    private readonly string _logFolder;
    private readonly TimeZoneInfo _zone;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Active> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeadlessSubmission> _waiting = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RunRecord> _last = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastLogs = new(StringComparer.OrdinalIgnoreCase);

    // Cards between leaving the queue (or ending a run that is resumed for its handoff) and their process starting: a
    // stop or an Open in a tab in that window cancels the start instead of being lost.
    private readonly HashSet<string> _starting = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _cancelled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HeadlessQueue _queue;

    /// <summary>Creates the runner.</summary>
    /// <param name="ledger">Where ended runs are recorded.</param>
    /// <param name="settings">The settings in force, read at each decision.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="start">Starts a run's process, calling back with each stdout line and then its exit code.</param>
    /// <param name="logFolder">Where each run's output is kept, one file per run.</param>
    /// <param name="zone">The zone whose calendar day the day limits count in.</param>
    public HeadlessRunner(
        IRunLedger ledger,
        Func<TheseusSettings> settings,
        ISystemClock clock,
        Func<ProcessStartRequest, Action<string>, Action<int>, IHeadlessProcess> start,
        string logFolder,
        TimeZoneInfo? zone = null)
    {
        _ledger = ledger;
        _settings = settings;
        _clock = clock;
        _start = start;
        _logFolder = logFolder;
        _zone = zone ?? TimeZoneInfo.Local;
        _queue = new HeadlessQueue(settings().MaxHeadlessRuns);

        foreach (var record in ledger.Read().Where(record => !record.IsHandoffRun))
        {
            _last[record.Workstream] = record;
        }
    }

    /// <summary>Raised, on any thread, whenever a run starts, writes a line, queues or ends.</summary>
    public event Action? Changed;

    /// <summary>Told each run as the ledger records it.</summary>
    public Action<RunRecord>? Ended { get; set; }

    /// <summary>Told a card's key and the breaker that tripped on it; the board pauses the card and shows the wait.</summary>
    public Action<string, BreakerTrip>? Tripped { get; set; }

    /// <summary>Told a queued card's key and why it could not start when its turn came.</summary>
    public Action<string, string>? Refused { get; set; }

    /// <summary>Reads a card's repositories' HEAD commits, before and after a run, to see whether it committed.</summary>
    public Func<IEnumerable<string>, IReadOnlyDictionary<string, string>> Heads { get; set; } = RunProgress.Heads;

    /// <summary>How many headless runs are going.</summary>
    public int Running => _queue.Running;

    /// <summary>How many are waiting for a slot.</summary>
    public int Queued => _queue.Queued;

    /// <summary>Why a card may not start headless now, or null when it may: a tripped breaker, or a day's budget spent.</summary>
    public string? Refusal(Workstream workstream) => Block(workstream) switch
    {
        null => null,
        { Reason: RefusalReason.BreakerTripped } block => $"{block.Title} is stopped: {block.Detail} Reset it from the card when fixed.",
        var block => $"{block.Title}: {block.Detail}",
    };

    /// <summary>
    /// The tripped breaker or spent limit that refuses a card's start now, or null: what the board says where Start was
    /// pressed, with the action that clears it (Nigel, 2026-11-25).
    /// </summary>
    public StartBlock? Block(Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        if (workstream.Breaker is { } trip)
        {
            return new StartBlock(workstream.Key, workstream.Title, RefusalReason.BreakerTripped, trip.Detail);
        }

        var verdict = Budget(workstream);

        return verdict.Allowed ? null : new StartBlock(workstream.Key, workstream.Title, RefusalReason.BudgetReached, verdict.Detail ?? "its budget is spent");
    }

    /// <summary>Starts a card's run now if a slot is free, or queues it.</summary>
    /// <returns>Null when it started; otherwise its place in the queue.</returns>
    public int? Submit(HeadlessSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var key = submission.Workstream.Key;
        _queue.Cap = _settings().MaxHeadlessRuns;

        int? position;

        lock (_gate)
        {
            position = _queue.Enter(key);

            if (position is not null)
            {
                _waiting[key] = submission;
            }
        }

        if (position is null)
        {
            Begin(submission, isHandoffRun: false);
        }
        else
        {
            TheseusLog.Information("Queued the headless run of {Workstream}: place {Position}", key, position);

            // A cap raised since the queue formed leaves slots free that no run's end would fill.
            Pump();
            position = _queue.PositionOf(key);
        }

        Raise();

        return position;
    }

    /// <summary>Whether a card has a run going or queued.</summary>
    public bool Holds(string workstream) => _queue.Holds(workstream);

    /// <summary>Whether a session is a headless run going now.</summary>
    public bool IsHeadless(string? sessionId)
    {
        lock (_gate)
        {
            return sessionId is not null && _active.Values.Any(active => string.Equals(active.Run.Start.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>What a card's face shows of its headless runs, or null when it has had none.</summary>
    public HeadlessCardState? StateOf(string workstream)
    {
        lock (_gate)
        {
            _last.TryGetValue(workstream, out var last);
            _lastLogs.TryGetValue(workstream, out var lastLog);

            if (_active.TryGetValue(workstream, out var active))
            {
                var run = active.Run;

                return new HeadlessCardState(true, null, run.StartedUtc, run.LastLine, run.Tokens, run.CostUsd, last, active.LogPath);
            }

            if (_queue.PositionOf(workstream) is { } position)
            {
                return new HeadlessCardState(false, position, null, null, RunTokens.None, null, last, lastLog);
            }

            return last is null ? null : new HeadlessCardState(false, null, null, null, last.Tokens, last.CostUsd, last, lastLog);
        }
    }

    /// <summary>Asks a card's headless run to write its handoff and stop: the owner's Stop.</summary>
    /// <returns>Whether the request reached a run whose turn was going; false when none was.</returns>
    public bool AskToStop(string workstream)
    {
        lock (_gate)
        {
            if (_waiting.Remove(workstream))
            {
                _queue.Leave(workstream);
                Raise();

                return true;
            }

            if (_starting.Contains(workstream))
            {
                _cancelled.Add(workstream);

                return true;
            }

            if (!_active.TryGetValue(workstream, out var active))
            {
                return false;
            }

            lock (active)
            {
                return active.Run.AskToStop();
            }
        }
    }

    /// <summary>Ends a card's run at once for Open in a tab, and completes when its process has exited.</summary>
    public Task InterruptAsync(string workstream)
    {
        lock (_gate)
        {
            if (_waiting.Remove(workstream))
            {
                _queue.Leave(workstream);
                Raise();

                return Task.CompletedTask;
            }

            if (_starting.Contains(workstream))
            {
                _cancelled.Add(workstream);

                return Task.CompletedTask;
            }

            if (!_active.TryGetValue(workstream, out var active))
            {
                return Task.CompletedTask;
            }

            lock (active)
            {
                active.Run.Interrupt(RunOutcome.Interrupted, _clock.UtcNow);
                active.OwnerTookOver = true;
            }

            return active.Exited.Task;
        }
    }

    /// <summary>Told every Theseus tool call, so a run that writes a record counts as making progress.</summary>
    public void ToolCalled(string sessionId, string tool)
    {
        if (!RunProgress.RecordTools.Contains(tool))
        {
            return;
        }

        lock (_gate)
        {
            foreach (var active in _active.Values.Where(active => string.Equals(active.Run.Start.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)))
            {
                active.RecordCalls++;
            }
        }
    }

    /// <summary>Checks every run going against its limits; called every few seconds.</summary>
    public void Tick()
    {
        List<Active> active;

        lock (_gate)
        {
            active = [.. _active.Values];
        }

        var now = _clock.UtcNow;

        foreach (var each in active)
        {
            HeadlessRunAction action;

            lock (each)
            {
                action = each.Run.Tick(now);
            }

            if (action == HeadlessRunAction.EndProcess)
            {
                TheseusLog.Warning("Ending the headless run of {Workstream} ({Run}): it began no session in time, or would not exit after its interrupt",
                    each.Run.Start.Workstream, each.Run.Start.RunId);
                each.Process?.End();
            }
        }

        if (active.Count > 0)
        {
            Raise();
        }
    }

    private BudgetVerdict Budget(Workstream workstream) =>
        DayBudget.Check(_settings().BudgetsFor(workstream.Project), _ledger.Read(), workstream.Key, workstream.Project,
            _clock.UtcNow, _zone);

    private void Begin(HeadlessSubmission submission, bool isHandoffRun, string? handoffPrompt = null)
    {
        var key = submission.Workstream.Key;

        lock (_gate)
        {
            _starting.Add(key);
        }

        Active? active;
        ProcessStartRequest? request;

        try
        {
            (active, request) = Prepare(submission, isHandoffRun, handoffPrompt);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            TheseusLog.Warning(exception, "The headless run of {Workstream} could not be prepared", key);
            Refused?.Invoke(key, $"{submission.Workstream.Title} could not start headless: {exception.Message}");
            (active, request) = (null, null);
        }

        bool cancelled;

        lock (_gate)
        {
            _starting.Remove(key);
            cancelled = _cancelled.Remove(key);

            if (active is not null && !cancelled)
            {
                _active[key] = active;
                _lastLogs[key] = active.LogPath;
            }
        }

        if (active is null || request is null || cancelled)
        {
            if (cancelled)
            {
                TheseusLog.Information("Did not start the headless run of {Workstream}: it was stopped or opened in a tab as it was starting", key);
            }

            Release(key);

            return;
        }

        try
        {
            var process = _start(request, line => Line(active, line), code => Exited(active, code));
            active.Process = process;
            active.Input.Attach(process);

            TheseusLog.Information(
                "Started {Workstream} headless as {Mode}: run {Run}, session {Session}, process {Process}, cost cap {Cap}, log {Log}",
                key, active.Mode, active.Run.Start.RunId, active.Run.Start.SessionId, process.ProcessId,
                Money.Usd(active.Run.Start.Limits.CostUsd), active.LogPath);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            TheseusLog.Warning(exception, "The headless run of {Workstream} would not start", key);
            active.StartError = exception.Message;
            Exited(active, -1);

            return;
        }

        lock (active)
        {
            active.Run.Begin(active.Prompt);
        }
    }

    // Everything a run needs before its process starts; null when its budget refuses it. Nothing here is visible to the
    // board until Begin publishes it, with its run already made.
    private (Active?, ProcessStartRequest?) Prepare(HeadlessSubmission submission, bool isHandoffRun, string? handoffPrompt)
    {
        var workstream = submission.Workstream;
        var settings = _settings();
        RunLimits limits;

        if (isHandoffRun)
        {
            var allowance = settings.BudgetsFor(workstream.Project).HandoffAllowanceUsd;
            limits = new RunLimits(allowance, long.MaxValue / 200, TimeSpan.FromMinutes(30), BudgetLimit.RunCost, allowance, 100);
        }
        else if (Budget(workstream) is { Limits: { } allowed })
        {
            limits = allowed;
        }
        else
        {
            var detail = Refusal(workstream) ?? "its budget is spent";
            TheseusLog.Information("Did not start the headless run of {Workstream}: {Detail}", workstream.Key, detail);
            Refused?.Invoke(workstream.Key, detail);

            return (null, null);
        }

        var spec = submission.Spec with { Headless = true, MaxBudgetUsd = Math.Max(0.01m, limits.CostUsd) };

        if (isHandoffRun)
        {
            spec = spec with { Mode = LaunchMode.Resume, Prompt = handoffPrompt ?? submission.StopText };
        }

        var start = new HeadlessRunStart(Guid.NewGuid(), workstream.Key, workstream.Project, spec.SessionId, submission.Reason,
            limits, submission.StopText)
        {
            Piece = submission.Piece,
            IsHandoffRun = isHandoffRun,
            OverTimeGrace = TimeSpan.FromMinutes(settings.Breakers.OverTimeGraceMinutes),
        };

        Directory.CreateDirectory(_logFolder);
        var folders = new[] { workstream.WorkingDirectory }.Concat(workstream.AddDirectories).ToList();
        var input = new BufferedInput();
        var active = new Active(submission, Path.Combine(_logFolder, $"{start.RunId:N}.jsonl"), Heads(folders), folders,
            new HeadlessRun(start, input, _clock.UtcNow), input, spec.Prompt, spec.Mode);

        var request = new ProcessStartRequest(spec.ClaudeExecutablePath, ClaudeArgumentBuilder.Build(spec), spec.WorkingDirectory)
        {
            Environment = spec.Environment,
        };

        return (active, request);
    }

    private void Line(Active active, string line)
    {
        lock (active)
        {
            try
            {
                File.AppendAllText(active.LogPath, line + "\n", Utf8);
            }
            catch (IOException exception)
            {
                TheseusLog.Warning(exception, "Could not keep a line of the headless run's output in {Log}", active.LogPath);
            }

            active.Run.Observe(line);
        }

        Raise();
    }

    private void Exited(Active active, int exitCode)
    {
        var now = _clock.UtcNow;
        var key = active.Submission.Workstream.Key;
        RunRecord? record = null;
        var handoff = false;

        // Whatever goes wrong here, the slot is freed and Open in a tab is released: a card left holding a slot would be
        // refused every start until Theseus restarts.
        try
        {
            var progress = RunProgress.Made(active.HeadsBefore, Heads(active.Folders), active.RecordCalls);

            lock (active)
            {
                record = active.Run.Ended(exitCode, now, progress, active.StartError ?? active.Process?.LastError);
            }

            try
            {
                _ledger.Append(record);
            }
            catch (IOException exception)
            {
                TheseusLog.Error(exception, "Could not record the headless run {Run} of {Workstream} in the ledger", record.RunId, key);
            }

            // Claude Code's own cap ends a run between calls without its handoff, so it is resumed once to write it.
            handoff = active.Run.HitCostCap && !active.Run.Start.IsHandoffRun && !active.OwnerTookOver;

            lock (_gate)
            {
                _active.Remove(key);

                if (!record.IsHandoffRun)
                {
                    _last[key] = record;
                }

                if (handoff)
                {
                    _starting.Add(key);
                }
            }

            TheseusLog.Information(
                "The headless run of {Workstream} ended {Outcome} (exit {Exit}): cost {Cost}, {Tokens} tokens, progress {Progress}{Detail}",
                key, record.Outcome, exitCode, record.CostUsd is { } cost ? Money.Usd(cost) : "not reported", record.Tokens.Counted,
                progress, record.LimitDetail is { } detail ? $", {detail}" : record.Error is { } error ? $", {error}" : string.Empty);

            Ended?.Invoke(record);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            TheseusLog.Error(exception, "Could not finish recording the headless run of {Workstream}", key);

            lock (_gate)
            {
                _active.Remove(key);
                _starting.Remove(key);
            }

            handoff = false;
        }
        finally
        {
            active.Exited.TrySetResult();
            (active.Process as IDisposable)?.Dispose();
        }

        if (handoff && record is not null)
        {
            Begin(active.Submission, isHandoffRun: true,
                $"Theseus: this run reached its cost limit ({record.LimitDetail?.Replace("Stopped at its limit: ", string.Empty, StringComparison.Ordinal)}). Do no more work. {active.Submission.StopText}");
            Raise();

            return;
        }

        if (!active.OwnerTookOver && record is not null)
        {
            Judge(active.Submission.Workstream, now);
        }

        Release(key);
    }

    private void Judge(Workstream workstream, DateTimeOffset now)
    {
        var since = workstream.BreakerResetUtc ?? DateTimeOffset.MinValue;
        var runs = _ledger.Read().Where(run => string.Equals(run.Workstream, workstream.Key, StringComparison.OrdinalIgnoreCase) && run.StartedUtc >= since);

        if (BreakerJudge.After(runs, _settings().Breakers, now) is { } trip)
        {
            TheseusLog.Warning("The breaker of {Workstream} tripped: {Detail}", workstream.Key, trip.Detail);
            Tripped?.Invoke(workstream.Key, trip);
        }
    }

    private void Release(string key)
    {
        _queue.Leave(key);
        Pump();
        Raise();
    }

    private void Pump()
    {
        _queue.Cap = _settings().MaxHeadlessRuns;

        while (_queue.TakeNext() is { } next)
        {
            HeadlessSubmission? submission;

            lock (_gate)
            {
                if (_waiting.Remove(next, out submission))
                {
                    _starting.Add(next);
                }
            }

            if (submission is not null)
            {
                Begin(submission, isHandoffRun: false);
            }
            else
            {
                _queue.Leave(next);
            }
        }
    }

    private void Raise() => Changed?.Invoke();

    private sealed class Active(
        HeadlessSubmission submission,
        string logPath,
        IReadOnlyDictionary<string, string> headsBefore,
        IReadOnlyList<string> folders,
        HeadlessRun run,
        BufferedInput input,
        string prompt,
        LaunchMode mode)
    {
        public HeadlessSubmission Submission { get; } = submission;

        public string LogPath { get; } = logPath;

        public IReadOnlyDictionary<string, string> HeadsBefore { get; } = headsBefore;

        public IReadOnlyList<string> Folders { get; } = folders;

        public HeadlessRun Run { get; } = run;

        public BufferedInput Input { get; } = input;

        public string Prompt { get; } = prompt;

        public LaunchMode Mode { get; } = mode;

        public IHeadlessProcess? Process { get; set; }

        public string? StartError { get; set; }

        public int RecordCalls { get; set; }

        public bool OwnerTookOver { get; set; }

        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // The run is created before its process, so the first line it writes has a run to read it; what the run writes
    // before the process exists is held until it does.
    private sealed class BufferedInput : IHeadlessInput
    {
        private readonly Lock _gate = new();
        private readonly List<string> _held = [];
        private IHeadlessInput? _target;
        private bool _closed;

        public void Attach(IHeadlessInput target)
        {
            lock (_gate)
            {
                _target = target;

                foreach (var line in _held)
                {
                    target.WriteLine(line);
                }

                _held.Clear();

                if (_closed)
                {
                    target.Close();
                }
            }
        }

        public void WriteLine(string line)
        {
            lock (_gate)
            {
                if (_target is null)
                {
                    _held.Add(line);
                }
                else
                {
                    _target.WriteLine(line);
                }
            }
        }

        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
                _target?.Close();
            }
        }
    }
}
