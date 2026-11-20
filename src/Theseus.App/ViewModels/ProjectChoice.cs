using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>One item of the Project menu: a project with its slugs under it, a slug, or Not in a project.</summary>
/// <param name="Header">What the item says, with any underscore doubled so it is not read as an access key.</param>
/// <param name="IsChecked">Whether it is the card's present membership.</param>
/// <param name="Command">What choosing it does, or null for a project, which only opens its slugs.</param>
/// <param name="Children">A project's slugs; empty for the others.</param>
/// <param name="ToolTip">More about it: the workstream and objective a slug is for, or why a project cannot be read.</param>
/// <param name="IsEnabled">False for a project that cannot be read, which is listed so it is not silently missing.</param>
public sealed record ProjectChoice(
    string Header,
    bool IsChecked,
    ICommand? Command,
    IReadOnlyList<ProjectChoice> Children,
    string? ToolTip,
    bool IsEnabled = true)
{
    /// <summary>Whether it shows a tick box: a choice does, a project that only opens its slugs does not.</summary>
    public bool IsCheckable => Command is not null;

    /// <summary>
    /// Equal when it shows the same, whatever its command: a refresh builds new commands for an unchanged menu, and
    /// replacing the menu then would close a submenu the owner has open.
    /// </summary>
    public bool Equals(ProjectChoice? other) =>
        other is not null
        && Header == other.Header && IsChecked == other.IsChecked && IsCheckable == other.IsCheckable
        && ToolTip == other.ToolTip && IsEnabled == other.IsEnabled
        && Children.SequenceEqual(other.Children);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Header, IsChecked, ToolTip, IsEnabled, Children.Count);
}
