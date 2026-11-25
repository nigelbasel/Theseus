namespace Theseus.App.ViewModels;

/// <summary>
/// A box or heading of the project view that folds, and the name its fold is remembered under, so Collapse all, Expand
/// all and Open what needs attention treat boxes and headings alike (1.1d AC 33, 48, 50).
/// </summary>
internal interface IFoldable
{
    /// <summary>The name of its entry in the view's folds.</summary>
    string FoldName { get; }

    /// <summary>Whether it is open with no entry of its own and no <c>*</c>.</summary>
    bool StartsOpen { get; }

    /// <summary>Whether it is open.</summary>
    bool IsOpen { get; set; }
}
