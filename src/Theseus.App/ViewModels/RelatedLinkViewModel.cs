using System.Diagnostics;
using System.IO;
using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>
/// One link in the drawer's Related section: what it says, the document it names, and, for an input
/// still waiting, Mark satisfied with a reason. Removing asks twice.
/// </summary>
public sealed class RelatedLinkViewModel : ObservableObject
{
    private readonly Func<string, string, string?> _satisfy;
    private readonly Func<string, string?> _remove;
    private string _reason = "Satisfied by hand";
    private string _refusal = string.Empty;
    private bool _isConfirmingRemove;

    /// <summary>Creates the row for one link.</summary>
    /// <param name="satisfy">Accepts the input by hand for a reason, returning why not if it was refused.</param>
    /// <param name="remove">Removes the link, returning why not if it was refused.</param>
    /// <param name="otherKey">The card at the other end of the link.</param>
    /// <param name="otherTitle">That card's title.</param>
    /// <param name="showOther">Opens that card's details.</param>
    public RelatedLinkViewModel(Link link, string description, LinkState state,
        Func<string, string, string?> satisfy, Func<string, string?> remove,
        string otherKey, string otherTitle, RelayCommand showOther)
    {
        Link = link;
        Description = description;
        State = state;
        OtherKey = otherKey;
        OtherTitle = otherTitle;
        ShowOtherCommand = showOther;
        _satisfy = satisfy;
        _remove = remove;

        OpenDocumentCommand = new RelayCommand(_ => Open(Document), _ => Document is { } path && File.Exists(path));
        SatisfyCommand = new RelayCommand(_ => Refusal = _satisfy(link.Id, Reason) ?? string.Empty,
            _ => CanSatisfy && !string.IsNullOrWhiteSpace(Reason));
        AskRemoveCommand = new RelayCommand(_ => IsConfirmingRemove = true);
        ConfirmRemoveCommand = new RelayCommand(_ =>
        {
            IsConfirmingRemove = false;
            Refusal = _remove(link.Id) ?? string.Empty;
        });
        KeepCommand = new RelayCommand(_ => IsConfirmingRemove = false);
    }

    /// <summary>The link this row shows.</summary>
    public Link Link { get; }

    /// <summary>The link in words, from the card being looked at.</summary>
    public string Description { get; }

    /// <summary>Where the link stands.</summary>
    public LinkState State { get; }

    /// <summary>The card at the other end of the link.</summary>
    public string OtherKey { get; }

    /// <summary>That card's title, which is all a row in the Links tab needs to say.</summary>
    public string OtherTitle { get; }

    /// <summary>Opens the other card's details.</summary>
    public RelayCommand ShowOtherCommand { get; }

    /// <summary>The state in words.</summary>
    public string StateText => State switch
    {
        LinkState.Waiting => "⧗ waiting",
        LinkState.Satisfied => $"✓ {Link.SatisfiedBy}",
        LinkState.CardMissing => "card missing",
        _ => string.Empty,
    };

    /// <summary>The document the link names: the output for an input, the brief for a spawned link.</summary>
    public string? Document => Link.Output ?? Link.Brief;

    /// <summary>Whether this input is still waiting, so it can be satisfied by hand.</summary>
    public bool CanSatisfy => State == LinkState.Waiting;

    /// <summary>Why it is satisfied, kept with the link so a hand-satisfied link can be told apart later.</summary>
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

    /// <summary>Whether Remove has been pressed once and waits to be confirmed.</summary>
    public bool IsConfirmingRemove
    {
        get => _isConfirmingRemove;
        private set
        {
            if (Set(ref _isConfirmingRemove, value))
            {
                Raise(nameof(CanAskRemove));
            }
        }
    }

    /// <summary>Whether Remove link is offered, which it is until it has been pressed once.</summary>
    public bool CanAskRemove => !_isConfirmingRemove;

    /// <summary>Opens the document the link names.</summary>
    public RelayCommand OpenDocumentCommand { get; }

    /// <summary>Accepts the input by hand.</summary>
    public RelayCommand SatisfyCommand { get; }

    /// <summary>Asks to remove the link.</summary>
    public RelayCommand AskRemoveCommand { get; }

    /// <summary>Removes the link.</summary>
    public RelayCommand ConfirmRemoveCommand { get; }

    /// <summary>Keeps it after all.</summary>
    public RelayCommand KeepCommand { get; }

    private static void Open(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
