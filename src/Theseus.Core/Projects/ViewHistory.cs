namespace Theseus.Core.Projects;

/// <summary>
/// Back and Forward through the places the owner jumped to (1.1f I46, D14): a link click, a tab switch, entering a project
/// from the board. Back from a project's first view returns to the board. Esc clears a selection without a jump, and
/// going somewhere new after Back drops what Forward would have reached, as a browser does.
/// </summary>
public sealed class ViewHistory
{
    private readonly List<ViewPlace> _places = [ViewPlace.Board];
    private int _at;

    /// <summary>Where the owner is.</summary>
    public ViewPlace Current => _places[_at];

    /// <summary>Whether Back has somewhere to go.</summary>
    public bool CanGoBack => _at > 0;

    /// <summary>Whether Forward has somewhere to go.</summary>
    public bool CanGoForward => _at < _places.Count - 1;

    /// <summary>Records a jump; a jump to where the owner already is records nothing.</summary>
    public void Jump(ViewPlace place)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place == Current)
        {
            return;
        }

        _places.RemoveRange(_at + 1, _places.Count - _at - 1);
        _places.Add(place);
        _at = _places.Count - 1;
    }

    /// <summary>Records where the owner is now without a jump, as when Esc clears the selection.</summary>
    public void Replace(ViewPlace place)
    {
        ArgumentNullException.ThrowIfNull(place);

        _places[_at] = place;
    }

    /// <summary>Goes back one place, and returns it; null when there is none.</summary>
    public ViewPlace? Back() => CanGoBack ? _places[--_at] : null;

    /// <summary>Goes forward one place, and returns it; null when there is none.</summary>
    public ViewPlace? Forward() => CanGoForward ? _places[++_at] : null;
}
