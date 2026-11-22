namespace Theseus.Core.Finding;

/// <summary>
/// The box's last few searches, most recent first, for its drop-down (item 8). Kept for this run only: the search is not
/// remembered between runs.
/// </summary>
public sealed class SearchHistory
{
    /// <summary>How many are kept.</summary>
    public const int Capacity = 8;

    private readonly List<string> _items = [];

    /// <summary>The searches, most recent first.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>Puts a search at the top, moving it there if it is already kept; blank text is not kept.</summary>
    public bool Add(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0 || (_items.Count > 0 && _items[0] == trimmed))
        {
            return false;
        }

        _items.Remove(trimmed);
        _items.Insert(0, trimmed);

        if (_items.Count > Capacity)
        {
            _items.RemoveAt(_items.Count - 1);
        }

        return true;
    }
}
