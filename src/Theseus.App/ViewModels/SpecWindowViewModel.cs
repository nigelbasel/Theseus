using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The spec window (1.1e AC 24a): a spec drawn as read, Preview or Source, with Accept spec… at its foot. Accept always
/// accepts the version on screen: a spec changed since it was drawn says so and is drawn again first.
/// </summary>
public sealed class SpecWindowViewModel : ObservableObject
{
    private readonly Func<SpecAtGate, SpecAtGate?> _readAgain;
    private readonly Func<SpecAtGate, string?> _whyNotAccept;
    private readonly Func<SpecAtGate, bool> _accept;
    private readonly Action<SpecAtGate> _openInEditor;
    private readonly Func<SpecAtGate, string?, string>? _reject;
    private bool _isRejecting;
    private string _note = string.Empty;
    private string? _rejectedVersion;
    private SpecAtGate _shown;
    private bool _isSource;
    private string _notice = string.Empty;
    private string _search = string.Empty;

    /// <summary>Creates the window's model for a spec as drawn.</summary>
    /// <param name="spec">The spec as it was read.</param>
    /// <param name="readAgain">The spec as it is on disk now, or null when it has gone.</param>
    /// <param name="whyNotAccept">Why Accept cannot run for a version, or null when it can.</param>
    /// <param name="accept">Accepts a version after its confirmation; whether it was accepted.</param>
    /// <param name="openInEditor">Opens its file in the editor.</param>
    /// <param name="reject">Rejects a version with the owner's note, or none; what was done, for the window's notice. Null
    /// leaves the window without Reject….</param>
    public SpecWindowViewModel(SpecAtGate spec, Func<SpecAtGate, SpecAtGate?> readAgain, Func<SpecAtGate, string?> whyNotAccept,
        Func<SpecAtGate, bool> accept, Action<SpecAtGate> openInEditor, Func<SpecAtGate, string?, string>? reject = null)
    {
        _shown = spec ?? throw new ArgumentNullException(nameof(spec));
        _readAgain = readAgain;
        _whyNotAccept = whyNotAccept;
        _accept = accept;
        _openInEditor = openInEditor;
        _reject = reject;
        PreviewCommand = new RelayCommand(_ => IsSource = false);
        SourceCommand = new RelayCommand(_ => IsSource = true);
        AcceptCommand = new RelayCommand(_ => Accept(), _ => CanAccept && !_isRejecting);
        AskRejectCommand = new RelayCommand(_ => IsRejecting = true, _ => CanReject && !_isRejecting);
        RejectCommand = new RelayCommand(_ => Reject(), _ => _isRejecting && CanReject);
        CancelRejectCommand = new RelayCommand(_ =>
        {
            IsRejecting = false;
            Note = string.Empty;
        }, _ => _isRejecting);
        OpenInEditorCommand = new RelayCommand(_ => _openInEditor(_shown), _ => _shown.FilePath is not null);
    }

    /// <summary>The spec on screen.</summary>
    public SpecAtGate Shown => _shown;

    /// <summary>The window's title: the piece of work and the version on screen.</summary>
    public string Title => $"{Noun} of {_shown.Slug} in {_shown.Project}, version {_shown.ShortVersion}";

    /// <summary>Its Markdown, as on screen.</summary>
    public string Text => _shown.Text;

    /// <summary>Whether the Markdown is shown as typed rather than drawn.</summary>
    public bool IsSource
    {
        get => _isSource;
        set
        {
            if (Set(ref _isSource, value))
            {
                Raise(nameof(IsPreview));
            }
        }
    }

    /// <summary>Whether it is drawn.</summary>
    public bool IsPreview => !_isSource;

    /// <summary>What is searched for (Ctrl+F).</summary>
    public string Search
    {
        get => _search;
        set => Set(ref _search, value ?? string.Empty);
    }

    /// <summary>What changed since it was opened, or why Accept did not run; empty when there is nothing to say.</summary>
    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    /// <summary>Whether Accept spec… can run for the version on screen.</summary>
    public bool CanAccept => WhyNot is null;

    /// <summary>Accept's tooltip: why it cannot run, or what it does.</summary>
    public string AcceptToolTip => WhyNot ?? $"Pass this gate for version {_shown.ShortVersion}, the one on screen: asks first";

    // A spec no longer on disk is not accepted (review of PR 5, finding 12).
    private string? WhyNot => _isGone
        ? $"The {Noun.ToLowerInvariant()} is no longer on disk, so there is nothing to accept"
        : _whyNotAccept(_shown);

    private bool _isGone;

    /// <summary>Shows it drawn.</summary>
    public RelayCommand PreviewCommand { get; }

    /// <summary>Shows it as typed.</summary>
    public RelayCommand SourceCommand { get; }

    /// <summary>Accept spec…: the version on screen, after its confirmation.</summary>
    public RelayCommand AcceptCommand { get; }

    /// <summary>Opens its file in the editor.</summary>
    public RelayCommand OpenInEditorCommand { get; }

    /// <summary>Reject…: shows the note and the button that rejects (board-small-changes item 4).</summary>
    public RelayCommand AskRejectCommand { get; }

    /// <summary>Rejects the version on screen with the note.</summary>
    public RelayCommand RejectCommand { get; }

    /// <summary>Puts the note away without rejecting.</summary>
    public RelayCommand CancelRejectCommand { get; }

    /// <summary>
    /// Whether Reject… can run: the version on screen can still be decided and was not rejected already, so its session is
    /// told once.
    /// </summary>
    public bool CanReject => _reject is not null && WhyNot is null && !IsRejectedOnScreen;

    private bool IsRejectedOnScreen => string.Equals(_rejectedVersion, _shown.ContentHash, StringComparison.Ordinal);

    /// <summary>Whether the window has Reject… at all.</summary>
    public bool HasReject => _reject is not null;

    /// <summary>Whether Reject… was pressed, so the note shows.</summary>
    public bool IsRejecting
    {
        get => _isRejecting;
        private set
        {
            if (Set(ref _isRejecting, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Why the owner rejects it, told to its session; optional.</summary>
    public string Note
    {
        get => _note;
        set => Set(ref _note, value ?? string.Empty);
    }

    /// <summary>Reject…'s tooltip: why it cannot run, or what it does.</summary>
    public string RejectToolTip => WhyNot
        ?? (IsRejectedOnScreen
            ? $"Version {_shown.ShortVersion} was rejected; its session is told to write a new one"
            : $"Send the {Noun.ToLowerInvariant()} back to its session to be written again, with a note; asks for the note first. Nothing is started");

    /// <summary>Raised when the text on screen has changed, so the window draws it again.</summary>
    public event EventHandler? Redrawn;

    private string Noun => _shown.IsRule ? "Rule" : "Spec";

    /// <summary>Reads the file again (it changed on disk, or the window came forward) and draws what is there now.</summary>
    public void Refresh()
    {
        if (_readAgain(_shown) is not { } now)
        {
            _isGone = true;
            Notice = $"The {Noun.ToLowerInvariant()} is no longer on disk; what is shown is version {_shown.ShortVersion}, as read.";
            RaiseState();

            return;
        }

        _isGone = false;

        if (!string.Equals(now.ContentHash, _shown.ContentHash, StringComparison.Ordinal))
        {
            var before = _shown.ShortVersion;
            Draw(now);
            Notice = $"The {Noun.ToLowerInvariant()} changed on disk: version {before} is now version {now.ShortVersion}, drawn again.";

            return;
        }

        RaiseState();
    }

    private void Accept()
    {
        // The file is read once more at the press: what is accepted is what is on screen, and nothing else.
        var now = _readAgain(_shown);

        if (now is null)
        {
            _isGone = true;
            Notice = $"The {Noun.ToLowerInvariant()} is no longer on disk, so it was not accepted.";
            RaiseState();

            return;
        }

        if (!string.Equals(now.ContentHash, _shown.ContentHash, StringComparison.Ordinal))
        {
            var before = _shown.ShortVersion;
            Draw(now);
            Notice = $"The {Noun.ToLowerInvariant()} changed since it was drawn (version {before}); it is drawn again as version "
                     + $"{now.ShortVersion}. Read it, then accept.";

            return;
        }

        if (_accept(_shown))
        {
            Notice = $"Accepted version {_shown.ShortVersion}.";
        }

        RaiseState();
    }

    private void Reject()
    {
        // As Accept: what is rejected is what is on screen, and nothing else.
        if (Changed("Read it, then decide.") || _reject is null)
        {
            return;
        }

        var note = string.IsNullOrWhiteSpace(_note) ? null : _note.Trim();
        var told = _reject(_shown, note);
        _rejectedVersion = _shown.ContentHash;
        IsRejecting = false;
        Note = string.Empty;
        Notice = $"Rejected version {_shown.ShortVersion}. {told}";
        RaiseState();
    }

    // Reads the file again; true, with the notice said, when it has gone or changed since it was drawn.
    private bool Changed(string then)
    {
        var now = _readAgain(_shown);

        if (now is null)
        {
            _isGone = true;
            Notice = $"The {Noun.ToLowerInvariant()} is no longer on disk, so nothing was decided.";
            RaiseState();

            return true;
        }

        if (string.Equals(now.ContentHash, _shown.ContentHash, StringComparison.Ordinal))
        {
            return false;
        }

        var before = _shown.ShortVersion;
        Draw(now);
        Notice = $"The {Noun.ToLowerInvariant()} changed since it was drawn (version {before}); it is drawn again as version "
                 + $"{now.ShortVersion}. {then}";

        return true;
    }

    private void Draw(SpecAtGate spec)
    {
        _shown = spec;
        Raise(nameof(Shown));
        Raise(nameof(Title));
        Raise(nameof(Text));
        RaiseState();
        Redrawn?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseState()
    {
        Raise(nameof(CanAccept));
        Raise(nameof(AcceptToolTip));
        Raise(nameof(CanReject));
        Raise(nameof(RejectToolTip));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
