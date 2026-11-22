using System.Collections.ObjectModel;
using Theseus.Core.Finding;

namespace Theseus.App.ViewModels;

/// <summary>
/// The Tags field in a card's Properties (card-search-and-tags item 10): the card's tags, each with ×, a box that offers
/// the tags already in use as you type, Enter to add. Used by a workstream card's drawer and a project card's Properties.
/// </summary>
public sealed class TagEditorViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<string>> _inUse;
    private readonly Action _changed;
    private string _newTag = string.Empty;

    /// <summary>Creates the field; <paramref name="inUse"/> gives the tags to offer, <paramref name="changed"/> runs on each add or remove.</summary>
    public TagEditorViewModel(Func<IReadOnlyList<string>> inUse, Action changed)
    {
        _inUse = inUse;
        _changed = changed;
        AddCommand = new RelayCommand(_ => Add(_newTag), _ => CanAdd);
        RemoveCommand = new RelayCommand(parameter => Remove(parameter as string), parameter => parameter is string);
        ChooseCommand = new RelayCommand(parameter => Add(parameter as string), parameter => parameter is string);
    }

    /// <summary>The card's tags, in the order given.</summary>
    public ObservableCollection<string> Tags { get; } = [];

    /// <summary>What is typed in the box.</summary>
    public string NewTag
    {
        get => _newTag;
        set
        {
            if (Set(ref _newTag, value ?? string.Empty))
            {
                Raise(nameof(CanAdd));
                Raise(nameof(AddToolTip));
                Raise(nameof(Suggestions));
            }
        }
    }

    /// <summary>Whether what is typed can be added: a valid tag the card does not have.</summary>
    public bool CanAdd => WhyNotAdd is null;

    /// <summary>Why Add cannot act, or null when it can (1.1e item 6b).</summary>
    public string? WhyNotAdd => TagNames.WhyNot(_newTag) is { } why ? why
        : Tags.Contains(TagNames.Normalise(_newTag)!, StringComparer.Ordinal) ? "The card already has this tag"
        : null;

    /// <summary>What Add says, enabled or not.</summary>
    public string AddToolTip => WhyNotAdd ?? $"Add #{TagNames.Normalise(_newTag)} (Enter)";

    /// <summary>The tags in use that contain what is typed and the card does not have, offered to choose (item 10).</summary>
    public IReadOnlyList<string> Suggestions
    {
        get
        {
            var typed = _newTag.Trim().TrimStart('#').ToLowerInvariant();

            return [.. _inUse()
                .Where(tag => !Tags.Contains(tag, StringComparer.Ordinal) && (typed.Length == 0 || tag.Contains(typed, StringComparison.Ordinal)))
                .Take(8)];
        }
    }

    /// <summary>Adds what is typed (Enter, or Add).</summary>
    public RelayCommand AddCommand { get; }

    /// <summary>× on a tag: takes it off.</summary>
    public RelayCommand RemoveCommand { get; }

    /// <summary>Adds an offered tag.</summary>
    public RelayCommand ChooseCommand { get; }

    /// <summary>Shows a card's tags without counting it as a change.</summary>
    public void Load(IEnumerable<string> tags)
    {
        Tags.Clear();

        foreach (var tag in tags)
        {
            Tags.Add(tag);
        }

        NewTag = string.Empty;
        Raise(nameof(Suggestions));
    }

    private void Add(string? typed)
    {
        if (TagNames.Normalise(typed) is not { } tag || Tags.Contains(tag, StringComparer.Ordinal))
        {
            return;
        }

        Tags.Add(tag);
        NewTag = string.Empty;
        Raise(nameof(Suggestions));
        _changed();
    }

    private void Remove(string? tag)
    {
        if (tag is not null && Tags.Remove(tag))
        {
            Raise(nameof(Suggestions));
            Raise(nameof(CanAdd));
            Raise(nameof(AddToolTip));
            _changed();
        }
    }
}
