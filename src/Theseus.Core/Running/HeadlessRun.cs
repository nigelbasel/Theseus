using Theseus.Core.Budgets;
using Theseus.Core.Sessions;

namespace Theseus.Core.Running;

/// <summary>
/// One headless run, from its first line on stdin to its process's exit: it reads the run's stream, counts its tokens,
/// watches its limits, and asks it to hand over and stop through stdin, so it ends at its next tool boundary and never
/// in the middle of a write (Nigel, 2026-11-24). A <c>result</c> ends a turn, not the run: a session
/// with a background task takes another turn when it finishes, so the run is over only when its process exits.
/// </summary>
public sealed class HeadlessRun
{
    private static readonly TimeSpan AfterInterrupt = TimeSpan.FromMinutes(2);

    private readonly IHeadlessInput _input;
    private readonly HashSet<string> _messages = new(StringComparer.Ordinal);
    private RunTokens _resultTokens = RunTokens.None;
    private RunTokens _turnTokens = RunTokens.None;
    private RunResultEvent? _lastResult;
    private RunOutcome? _interruptedAs;
    private DateTimeOffset? _interruptedUtc;

    /// <summary>Creates the run; nothing is written until <see cref="Begin"/>.</summary>
    public HeadlessRun(HeadlessRunStart start, IHeadlessInput input, DateTimeOffset startedUtc)
    {
        Start = start ?? throw new ArgumentNullException(nameof(start));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        StartedUtc = startedUtc;
    }

    /// <summary>What it was started with.</summary>
    public HeadlessRunStart Start { get; }

    /// <summary>When it started.</summary>
    public DateTimeOffset StartedUtc { get; }

    /// <summary>Whether Claude Code has begun its session.</summary>
    public bool HasBegun { get; private set; }

    /// <summary>Whether stdin is still open, so a message can still reach it.</summary>
    public bool IsInputOpen { get; private set; } = true;

    /// <summary>Its latest line of text or tool call, for the card.</summary>
    public string? LastLine { get; private set; }

    /// <summary>Tokens so far: from its results, and from the calls of a turn still going.</summary>
    public RunTokens Tokens { get; private set; } = RunTokens.None;

    /// <summary>Its cost so far, known only at the end of each turn; null before the first.</summary>
    public decimal? CostUsd => _lastResult?.TotalCostUsd;

    /// <summary>The limit it was asked to stop at, or null.</summary>
    public BudgetLimit? StoppedAt { get; private set; }

    /// <summary>The card's sentence for that limit.</summary>
    public string? StopDetail { get; private set; }

    /// <summary>
    /// Whether Claude Code's own cost cap ended it: it then stopped between calls without writing its handoff, so it is
    /// resumed once to write it.
    /// </summary>
    public bool HitCostCap { get; private set; }

    /// <summary>Whether it has been asked to stop, for any reason.</summary>
    public bool StopAsked { get; private set; }

    /// <summary>Gives the run its opening.</summary>
    public void Begin(string prompt) => _input.WriteLine(RunStreamParser.UserMessage(prompt));

    /// <summary>Reads one line of its output.</summary>
    public void Observe(string line)
    {
        switch (RunStreamParser.Parse(line))
        {
            case RunInitEvent:
                HasBegun = true;
                break;

            case RunAssistantEvent assistant:
                if (assistant.Line is { } text)
                {
                    LastLine = text;
                }

                if (assistant.MessageId is null || _messages.Add(assistant.MessageId))
                {
                    _turnTokens = _turnTokens.Plus(assistant.Usage);
                    Tokens = _resultTokens.Plus(_turnTokens);
                }

                break;

            case RunResultEvent result:
                _lastResult = result;

                // A turn ended by Claude Code's cost cap reports no usage (measured 2026-11-24), so its calls' own counts stand.
                _resultTokens = _resultTokens.Plus(result.Usage.Counted + result.Usage.CacheRead == 0 ? _turnTokens : result.Usage);
                _turnTokens = RunTokens.None;
                Tokens = _resultTokens;
                _messages.Clear();

                if (result.HitCostCap)
                {
                    HitCostCap = true;
                    StopAt(Start.Limits.CostLimit, CostDetail(result.TotalCostUsd));
                }

                CloseInput();
                break;
        }
    }

    /// <summary>Checks the run's limits against now.</summary>
    public HeadlessRunAction Tick(DateTimeOffset now)
    {
        var elapsed = now - StartedUtc;

        if (!HasBegun && elapsed >= Start.StartAllowance)
        {
            return HeadlessRunAction.EndProcess;
        }

        if (_interruptedUtc is { } interrupted)
        {
            return now - interrupted >= AfterInterrupt ? HeadlessRunAction.EndProcess : HeadlessRunAction.None;
        }

        var limits = Start.Limits;

        if (limits.Time is { } time && elapsed >= time + Start.OverTimeGrace)
        {
            Interrupt(RunOutcome.OverTime, now);

            return HeadlessRunAction.None;
        }

        if (!StopAsked && limits.Time is { } limit && elapsed >= Share(limit, limits.StopAtPercent))
        {
            AskToStop(BudgetLimit.RunTime,
                $"Stopped at its limit: run time {(int)elapsed.TotalMinutes} minutes of {(int)limit.TotalMinutes}.");
        }

        if (!StopAsked && limits.Tokens is { } tokens && Tokens.Counted >= tokens * limits.StopAtPercent / 100)
        {
            AskToStop(BudgetLimit.RunTokens,
                $"Stopped at its limit: run tokens {TokenCount.Format(Tokens.Counted)} of {TokenCount.Format(tokens)}.");
        }

        return HeadlessRunAction.None;
    }

    /// <summary>Asks the run to write its handoff and stop: the owner's Stop, through stdin.</summary>
    /// <returns>Whether the request reached it: false once its stdin is closed.</returns>
    public bool AskToStop() => Write(Start.StopText);

    /// <summary>Ends the run's turn at once, as Esc does in a tab, and closes its stdin.</summary>
    public void Interrupt(RunOutcome reason, DateTimeOffset now)
    {
        if (_interruptedAs is not null)
        {
            return;
        }

        _interruptedAs = reason;
        _interruptedUtc = now;

        if (IsInputOpen)
        {
            _input.WriteLine(RunStreamParser.Interrupt(Start.RunId.ToString("N")));
        }

        CloseInput();
    }

    /// <summary>The ledger's record of the run, once its process has exited.</summary>
    /// <param name="exitCode">The process's exit code.</param>
    /// <param name="now">When it exited.</param>
    /// <param name="madeProgress">Whether it made a commit or wrote a record while it ran.</param>
    /// <param name="errorOutput">The last line it wrote to stderr, for a crash with no result.</param>
    public RunRecord Ended(int exitCode, DateTimeOffset now, bool madeProgress, string? errorOutput = null)
    {
        var outcome = Outcome(exitCode);
        var error = outcome switch
        {
            RunOutcome.Failed => FirstLine(_lastResult?.Text) ?? FirstLine(_lastResult?.Subtype),
            RunOutcome.Crashed => FirstLine(errorOutput) ?? $"exit code {exitCode}",
            RunOutcome.FailedStart => FirstLine(errorOutput) ?? $"no session began within {(int)Start.StartAllowance.TotalSeconds} seconds",
            _ => null,
        };

        return new RunRecord(Start.RunId, Start.Workstream, Start.Project, Start.SessionId, Start.Reason, StartedUtc, now,
            outcome, CostUsd, Tokens)
        {
            Piece = Start.Piece,
            Limit = outcome == RunOutcome.StoppedAtLimit ? StoppedAt : null,
            LimitDetail = outcome == RunOutcome.StoppedAtLimit ? StopDetail : null,
            Error = error,
            MadeProgress = madeProgress,
            IsHandoffRun = Start.IsHandoffRun,
            ExitCode = exitCode,
        };
    }

    private RunOutcome Outcome(int exitCode)
    {
        if (_interruptedAs is { } interrupted)
        {
            return interrupted;
        }

        if (!HasBegun)
        {
            return RunOutcome.FailedStart;
        }

        if (StoppedAt is not null)
        {
            return RunOutcome.StoppedAtLimit;
        }

        if (_lastResult is null)
        {
            return exitCode == 0 ? RunOutcome.Completed : RunOutcome.Crashed;
        }

        return _lastResult.IsError ? RunOutcome.Failed : RunOutcome.Completed;
    }

    private void AskToStop(BudgetLimit limit, string detail)
    {
        if (Write($"Theseus: this run has reached {Start.Limits.StopAtPercent}% of a limit ({detail.Replace("Stopped at its limit: ", string.Empty, StringComparison.Ordinal)}). {Start.StopText}"))
        {
            StopAt(limit, detail);
        }
    }

    private void StopAt(BudgetLimit limit, string detail)
    {
        StoppedAt ??= limit;
        StopDetail ??= detail;
    }

    private string CostDetail(decimal? spent)
    {
        var limits = Start.Limits;
        var amount = Money.Usd(spent ?? limits.CostUsd ?? 0m);
        var full = Money.Usd(limits.CostLimitValue ?? spent ?? 0m);

        return limits.CostLimit switch
        {
            BudgetLimit.CardDay => $"Stopped at its limit: card's spend today reached its {full} a day (this run {amount}).",
            BudgetLimit.ProjectDay => $"Stopped at its limit: project's spend today reached its {full} a day (this run {amount}).",
            _ => $"Stopped at its limit: run cost {amount} of {full}.",
        };
    }

    private bool Write(string text)
    {
        StopAsked = true;

        if (!IsInputOpen)
        {
            return false;
        }

        _input.WriteLine(RunStreamParser.UserMessage(text));

        return true;
    }

    private void CloseInput()
    {
        if (!IsInputOpen)
        {
            return;
        }

        IsInputOpen = false;
        _input.Close();
    }

    private static TimeSpan Share(TimeSpan whole, int percent) => TimeSpan.FromTicks(whole.Ticks * percent / 100);

    private static string? FirstLine(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Split('\n')[0].Trim();
}
