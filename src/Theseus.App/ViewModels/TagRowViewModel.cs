using Theseus.Core.Finding;

namespace Theseus.App.ViewModels;

/// <summary>
/// One tag in Options, Tags (card-search-and-tags item 13): its count and colour, renamed on every card, or deleted from
/// every card after a confirmation naming the count.
/// </summary>
public sealed class TagRowViewModel : ObservableObject
{
    private readonly BoardTags _tags;
    private readonly Action _reload;
    private string _newName;
    private bool _isConfirmingDelete;

    /// <summary>Creates a row over the board's tags; <paramref name="reload"/> rebuilds the list after a rename or delete.</summary>
    public TagRowViewModel(TagCount use, BoardTags tags, Action reload)
    {
        Tag = use.Tag;
        Count = use.Count;
        _tags = tags;
        _reload = reload;
        _newName = use.Tag;
        Swatches = [.. TagPalette.Colours.Select(colour => new TagColourChoice(colour, new RelayCommand(_ => SetColour(colour))))];
        RenameCommand = new RelayCommand(_ => Rename(), _ => CanRename);
        DeleteCommand = new RelayCommand(_ => IsConfirmingDelete = true);
        ConfirmDeleteCommand = new RelayCommand(_ => Delete());
        CancelDeleteCommand = new RelayCommand(_ => IsConfirmingDelete = false);
    }

    /// <summary>The tag.</summary>
    public string Tag { get; }

    /// <summary>"#bym".</summary>
    public string Label => "#" + Tag;

    /// <summary>How many cards carry it.</summary>
    public int Count { get; }

    /// <summary>"7 cards".</summary>
    public string CountText => Count == 1 ? "1 card" : $"{Count} cards";

    /// <summary>Its colour now.</summary>
    public string ColourHex => _tags.ColourOf(Tag);

    /// <summary>The palette's colours, each choosing itself for the tag.</summary>
    public IReadOnlyList<TagColourChoice> Swatches { get; }

    /// <summary>The name typed for a rename.</summary>
    public string NewName
    {
        get => _newName;
        set
        {
            if (Set(ref _newName, value ?? string.Empty))
            {
                Raise(nameof(CanRename));
                Raise(nameof(RenameToolTip));
            }
        }
    }

    /// <summary>Whether Rename can act: a valid tag, other than this one.</summary>
    public bool CanRename => WhyNotRename is null;

    /// <summary>Why Rename cannot act, or null (1.1e item 6b).</summary>
    public string? WhyNotRename => TagNames.WhyNot(_newName) is { } why ? why
        : TagNames.Normalise(_newName) == Tag ? "Type a new name first"
        : null;

    /// <summary>What Rename says, enabled or not.</summary>
    public string RenameToolTip => WhyNotRename ?? $"Rename #{Tag} to #{TagNames.Normalise(_newName)} on {CountText}";

    /// <summary>Renames the tag on every card.</summary>
    public RelayCommand RenameCommand { get; }

    /// <summary>Asks before deleting.</summary>
    public RelayCommand DeleteCommand { get; }

    /// <summary>Deletes the tag from every card.</summary>
    public RelayCommand ConfirmDeleteCommand { get; }

    /// <summary>Keeps the tag.</summary>
    public RelayCommand CancelDeleteCommand { get; }

    /// <summary>Whether the delete is waiting for its confirmation.</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        private set => Set(ref _isConfirmingDelete, value);
    }

    /// <summary>The confirmation, naming the count (item 13).</summary>
    public string DeleteQuestion => $"Take #{Tag} off {CountText}? Its colour goes too.";

    private void SetColour(string colour)
    {
        _tags.SetColour(Tag, colour);
        Raise(nameof(ColourHex));
    }

    private void Rename()
    {
        if (TagNames.Normalise(_newName) is { } to)
        {
            _tags.Rename(Tag, to);
            _reload();
        }
    }

    private void Delete()
    {
        _tags.Delete(Tag);
        _reload();
    }
}
