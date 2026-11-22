namespace Theseus.App.ViewModels;

/// <summary>One tag in the Tags ▾ list beside the search box (card-search-and-tags item 12): its count, and a tick that filters by it.</summary>
public sealed class TagFilterRowViewModel : ObservableObject
{
    private readonly Action<string, bool> _tick;
    private bool _isTicked;

    /// <summary>Creates a row; <paramref name="tick"/> ticks or unticks the tag in the search.</summary>
    public TagFilterRowViewModel(string tag, int count, string colourHex, bool isTicked, Action<string, bool> tick)
    {
        Tag = tag;
        Count = count;
        ColourHex = colourHex;
        _isTicked = isTicked;
        _tick = tick;
    }

    /// <summary>The tag.</summary>
    public string Tag { get; }

    /// <summary>How many cards carry it.</summary>
    public int Count { get; }

    /// <summary>Its colour.</summary>
    public string ColourHex { get; }

    /// <summary>What the row says: "#bym  7".</summary>
    public string Label => $"#{Tag}";

    /// <summary>Whether the board is filtered by it.</summary>
    public bool IsTicked
    {
        get => _isTicked;
        set
        {
            if (Set(ref _isTicked, value))
            {
                _tick(Tag, value);
            }
        }
    }

    /// <summary>Follows a tick made elsewhere, such as clicking a tag on a card, without ticking again.</summary>
    internal void Show(bool ticked) => Set(ref _isTicked, ticked, nameof(IsTicked));
}
