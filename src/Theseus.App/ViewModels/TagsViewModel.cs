using System.Collections.ObjectModel;

namespace Theseus.App.ViewModels;

/// <summary>Options, Tags (card-search-and-tags item 13): every tag in use, with its count and colour, to rename or delete.</summary>
public sealed class TagsViewModel : ObservableObject
{
    private readonly BoardTags _tags;

    /// <summary>Creates the list over the board's tags.</summary>
    public TagsViewModel(BoardTags tags)
    {
        _tags = tags;
        Load();
    }

    /// <summary>One row per tag in use, alphabetically.</summary>
    public ObservableCollection<TagRowViewModel> Rows { get; } = [];

    /// <summary>Whether no card has a tag yet.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Reads the tags in use again, as Options opens and after a rename or delete.</summary>
    public void Load()
    {
        Rows.Clear();

        foreach (var use in _tags.InUse())
        {
            Rows.Add(new TagRowViewModel(use, _tags, Load));
        }

        Raise(nameof(IsEmpty));
    }
}
