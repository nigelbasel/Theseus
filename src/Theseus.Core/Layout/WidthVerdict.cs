namespace Theseus.Core.Layout;

/// <summary>How readable a terminal of a given width will be.</summary>
public enum WidthVerdict
{
    /// <summary>Wide enough that nothing wraps awkwardly.</summary>
    Comfortable,

    /// <summary>Eighty columns or so: everything fits, nothing is generous.</summary>
    Usable,

    /// <summary>Narrow enough that output starts folding badly.</summary>
    Cramped,

    /// <summary>Not worth arranging. Better to leave the window alone than to make it this size.</summary>
    Unusable,
}
