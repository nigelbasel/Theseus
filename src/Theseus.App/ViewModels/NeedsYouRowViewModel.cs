using System.Diagnostics;
using System.IO;
using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>
/// One row under Needs you in the Chains tab: a report waiting for Nigel's Confirm, or a report that
/// was never sent by a card he has finished with. Nothing here starts a session, and a report is
/// satisfied only by his press: Confirm for a <c>done</c> or <c>go</c> verdict, otherwise Mark
/// satisfied with a reason, prefilled from the verdict.
/// </summary>
public sealed class NeedsYouRowViewModel : ObservableObject
{
    private readonly Func<string, string?> _confirm;
    private readonly Func<string, string, string?> _satisfy;
    private string _reason = string.Empty;
    private string _refusal = string.Empty;
    private bool _isAskingReason;

    /// <summary>Creates the row for one link.</summary>
    /// <param name="heading">"Writer → reader", in titles.</param>
    /// <param name="detail">The verdict and document of a report, or why a report is lost.</param>
    /// <param name="colorHex">The writing card's colour, as the row's left border.</param>
    /// <param name="handoff">The writer's handoff, opened when its report is lost.</param>
    /// <param name="isSetAside">Whether Not yet was pressed on this report, which sorts it last.</param>
    /// <param name="confirm">Confirms the report, returning why not if it was refused.</param>
    /// <param name="satisfy">Satisfies the link by hand for a reason, returning why not if it was refused.</param>
    /// <param name="notYet">Sets the report aside until it changes.</param>
    public NeedsYouRowViewModel(Link link, LinkState state, string heading, string detail, string colorHex,
        string? handoff, bool isSetAside, Func<string, string?> confirm, Func<string, string, string?> satisfy,
        Action<NeedsYouRowViewModel> notYet)
    {
        Link = link;
        State = state;
        Heading = heading;
        Detail = detail;
        ColorHex = colorHex;
        Handoff = handoff;
        IsSetAside = isSetAside;
        _confirm = confirm;
        _satisfy = satisfy;

        OpenReportCommand = new RelayCommand(_ => Open(link.LatestReport?.Document),
            _ => link.LatestReport?.Document is { } path && File.Exists(path));
        OpenHandoffCommand = new RelayCommand(_ => Open(handoff), _ => handoff is { } path && File.Exists(path));
        ConfirmCommand = new RelayCommand(_ => Confirm());
        AskReasonCommand = new RelayCommand(_ =>
        {
            Reason = "Satisfied by hand";
            IsAskingReason = true;
        });
        SatisfyCommand = new RelayCommand(_ => Refusal = _satisfy(link.Id, Reason) ?? string.Empty,
            _ => !string.IsNullOrWhiteSpace(Reason));
        CancelReasonCommand = new RelayCommand(_ => IsAskingReason = false);
        NotYetCommand = new RelayCommand(_ => notYet(this), _ => !IsSetAside);
    }

    /// <summary>The link this row is about.</summary>
    public Link Link { get; }

    /// <summary>Reported or ReportLost.</summary>
    public LinkState State { get; }

    /// <summary>Whether this is a report waiting to be confirmed, rather than a lost one.</summary>
    public bool IsReported => State == LinkState.Reported;

    /// <summary>Whether this is a lost report.</summary>
    public bool IsLost => State == LinkState.ReportLost;

    /// <summary>The glyph before the heading: ? for a report to confirm, ! for a lost one.</summary>
    public string Glyph => IsReported ? "?" : "!";

    /// <summary>"Writer → reader".</summary>
    public string Heading { get; }

    /// <summary>The verdict, time and document of a report, or why it is lost.</summary>
    public string Detail { get; }

    /// <summary>The writer's colour.</summary>
    public string ColorHex { get; }

    /// <summary>The writer's handoff.</summary>
    public string? Handoff { get; }

    /// <summary>When the report arrived and the session that sent it, for the tooltip.</summary>
    public string Evidence => Link.LatestReport is { } report
        ? $"Agent-originated: sent by session {report.SessionId} at {report.AtUtc.ToLocalTime():yyyy-MM-dd HH:mm}.{Environment.NewLine}{report.Summary}"
        : "No report was sent.";

    /// <summary>Whether Not yet was pressed, so it sorts below the others.</summary>
    public bool IsSetAside { get; }

    /// <summary>Whether the reason box for Mark satisfied is showing.</summary>
    public bool IsAskingReason
    {
        get => _isAskingReason;
        private set => Set(ref _isAskingReason, value);
    }

    /// <summary>Why the link is satisfied, kept with it.</summary>
    public string Reason
    {
        get => _reason;
        set => Set(ref _reason, value);
    }

    /// <summary>Why the last change was refused, or nothing.</summary>
    public string Refusal
    {
        get => _refusal;
        private set => Set(ref _refusal, value);
    }

    /// <summary>Opens the reported document.</summary>
    public RelayCommand OpenReportCommand { get; }

    /// <summary>Opens the writer's handoff.</summary>
    public RelayCommand OpenHandoffCommand { get; }

    /// <summary>Confirms a <c>done</c> or <c>go</c> report at once; any other opens the reason box with the verdict in it.</summary>
    public RelayCommand ConfirmCommand { get; }

    /// <summary>Opens the reason box for Mark satisfied.</summary>
    public RelayCommand AskReasonCommand { get; }

    /// <summary>Satisfies the link for the reason given.</summary>
    public RelayCommand SatisfyCommand { get; }

    /// <summary>Closes the reason box.</summary>
    public RelayCommand CancelReasonCommand { get; }

    /// <summary>Leaves the report as it is and moves it below the others.</summary>
    public RelayCommand NotYetCommand { get; }

    private void Confirm()
    {
        var verdict = Link.LatestReport?.Verdict;

        if (ReportVerdicts.ConfirmsInOnePress(verdict))
        {
            Refusal = _confirm(Link.Id) ?? string.Empty;
            return;
        }

        Reason = verdict is null ? "Report read; no verdict given" : $"Report read; verdict {verdict}";
        IsAskingReason = true;
    }

    private static void Open(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
