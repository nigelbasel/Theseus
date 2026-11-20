using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One slug in a cell of the project view: its card, with the board's face and menu, or, for a slug with no card, a
/// line saying where it stands (AC 4, 15). Its spec gate's Waiting on you, with Open spec and Accept spec, sits on it.
/// </summary>
public sealed class ProjectEntryViewModel : ObservableObject
{
    private string _source;

    /// <summary>Creates an entry.</summary>
    public ProjectEntryViewModel(string slug, string label, WorkstreamCardViewModel? card, WorkstreamStage stage, bool isVerifier)
    {
        Slug = slug;
        Label = label;
        Card = card;
        Stage = stage;
        IsVerifier = isVerifier;
        _source = stage.Source.Describe;
    }

    /// <summary>
    /// Where its work was read, branch and commit (AC 5). Kept live: a new commit changes it in place rather than
    /// rebuilding the view (PR 2 review, item 4).
    /// </summary>
    public string Source
    {
        get => _source;
        set
        {
            if (Set(ref _source, value))
            {
                Raise(nameof(ToolTipText));
            }
        }
    }

    /// <summary>Its tooltip: the gates passed, then where it was read.</summary>
    public string ToolTipText => Detail.Length == 0 ? _source : $"{Detail}{Environment.NewLine}{_source}";

    /// <summary>The slug it is for; empty for a card in No slug yet.</summary>
    public string Slug { get; }

    /// <summary>What a slug with no card shows instead of a card: its slug, workstream and where it stands.</summary>
    public string Label { get; }

    /// <summary>The card, or null for a slug with no card.</summary>
    public WorkstreamCardViewModel? Card { get; }

    /// <summary>Whether it shows a card.</summary>
    public bool HasCard => Card is not null;

    /// <summary>Where its work stands.</summary>
    public WorkstreamStage Stage { get; }

    /// <summary>Whether it is a verifier card, which sits in verify whatever the artefacts say (AC 10).</summary>
    public bool IsVerifier { get; }

    /// <summary>What is said under it: ahead of your gate, before method-v0.2, no evidence, not started; or empty.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>Whether a note is shown.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>The gates passed and the source it was read from, for its tooltip (AC 5, 7).</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Whether its spec (a bug's rule) is reached and waiting on the owner (AC 15).</summary>
    public bool IsWaitingOnYou { get; init; }

    /// <summary>What Waiting on you says: plain, or that the spec changed since he accepted it.</summary>
    public string WaitingText { get; init; } = "Waiting on you";

    /// <summary>Opens the spec as it was read.</summary>
    public ICommand? OpenSpecCommand { get; init; }

    /// <summary>Accepts the spec, after a confirmation naming the slug and the commit.</summary>
    public ICommand? AcceptSpecCommand { get; init; }
}
