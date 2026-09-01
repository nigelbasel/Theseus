using System.Collections.ObjectModel;
using Theseus.App.Services;
using Theseus.Core.Layout;

namespace Theseus.App.ViewModels;

/// <summary>
/// The chooser: which terminal windows take part, how they should be placed, and what that will
/// come to before it is done.
///
/// Saying what the result will be, before applying it, is the point of having a window at all. Four
/// terminals tiled across this display are sixty columns wide and unreadable, and finding that out
/// by doing it - after your own arrangement has been destroyed - is a poor way to learn it.
/// </summary>
public sealed class ArrangeViewModel : ObservableObject
{
    private readonly WindowArranger _arranger = new();
    private ArrangementKind _kind = ArrangementKind.Tile;
    private bool _isOpen;
    private string _outcome = string.Empty;

    /// <summary>Creates the chooser.</summary>
    public ArrangeViewModel()
    {
        OpenCommand = new RelayCommand(_ =>
        {
            Look();
            IsOpen = true;
        });

        CloseCommand = new RelayCommand(_ => IsOpen = false);
        RefreshCommand = new RelayCommand(_ => Look());
        ApplyCommand = new RelayCommand(_ => Apply(), _ => Chosen().Count > 0);
        RestoreCommand = new RelayCommand(_ => Restore(), _ => _arranger.CanRestore);
    }

    /// <summary>The terminal windows on the desktop, each tickable.</summary>
    public ObservableCollection<ArrangeGroupViewModel> Groups { get; } = [];

    /// <summary>Shows the chooser, having looked at the desktop first.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Hides it.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Looks at the desktop again.</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>Places the ticked windows.</summary>
    public RelayCommand ApplyCommand { get; }

    /// <summary>Puts everything back where it was.</summary>
    public RelayCommand RestoreCommand { get; }

    /// <summary>Whether the chooser is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>Which arrangement to apply.</summary>
    public ArrangementKind Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                Raise(nameof(Preview));
            }
        }
    }

    /// <summary>Every arrangement, for the chooser to offer.</summary>
    public static IReadOnlyList<ArrangementKind> Kinds { get; } = [.. Enum.GetValues<ArrangementKind>()];

    /// <summary>What applying this would come to, worked out before anything moves.</summary>
    public string Preview
    {
        get
        {
            var chosen = Chosen();

            if (chosen.Count == 0)
            {
                return "Nothing ticked.";
            }

            var area = TerminalWindows.WorkAreaFor(chosen[0].Window.Handle);

            if (area.Width <= 0)
            {
                return "The monitor could not be measured.";
            }

            var plan = LayoutPlanner.Plan(Kind, area, chosen.Count);
            var narrowest = plan.Min(rect => rect.Width);
            var windows = chosen.Count == 1 ? "1 window" : $"{chosen.Count} windows";

            if (Kind is not ArrangementKind.Tile)
            {
                return $"{windows}, {Kind.ToString().ToLowerInvariant()}d, "
                       + $"{narrowest}px wide — {ReadableWidth.Describe(narrowest)}";
            }

            var (columns, rows) = TileShape.For(chosen.Count, area);

            return $"{windows} in a {columns}×{rows}, {narrowest}px wide — "
                   + ReadableWidth.Describe(narrowest);
        }
    }

    /// <summary>Whether the arrangement would leave windows too narrow to work in.</summary>
    public bool IsTooNarrow
    {
        get
        {
            var chosen = Chosen();

            if (chosen.Count == 0)
            {
                return false;
            }

            var area = TerminalWindows.WorkAreaFor(chosen[0].Window.Handle);

            return area.Width > 0
                   && LayoutPlanner.Plan(Kind, area, chosen.Count).Min(rect => rect.Width) is var narrowest
                   && ReadableWidth.Of(narrowest) is WidthVerdict.Cramped or WidthVerdict.Unusable;
        }
    }

    /// <summary>What the last apply or restore did.</summary>
    public string Outcome
    {
        get => _outcome;
        private set => Set(ref _outcome, value);
    }

    /// <summary>Reads the desktop and rebuilds the list.</summary>
    public void Look()
    {
        // Two sets, not one. Knowing which windows were ticked is not enough to tell a window that
        // was deliberately unticked from one nobody has seen before, and those want opposite answers.
        var known = Groups.Select(group => group.Window.Handle).ToHashSet();
        var ticked = Groups.Where(group => group.IsIncluded).Select(group => group.Window.Handle).ToHashSet();

        Groups.Clear();

        foreach (var window in TerminalWindows.All())
        {
            // A window seen before keeps whatever was decided about it. A new one starts ticked:
            // somebody who has just opened a terminal probably wants it arranged with the rest.
            var group = new ArrangeGroupViewModel(window)
            {
                IsIncluded = !known.Contains(window.Handle) || ticked.Contains(window.Handle),
            };

            group.Included += (_, _) => PreviewChanged();
            Groups.Add(group);
        }

        PreviewChanged();
    }

    private void PreviewChanged()
    {
        Raise(nameof(Preview));
        Raise(nameof(IsTooNarrow));
    }

    private IReadOnlyList<ArrangeGroupViewModel> Chosen() =>
        [.. Groups.Where(group => group.IsIncluded)];

    private void Apply()
    {
        var chosen = Chosen();
        var area = TerminalWindows.WorkAreaFor(chosen[0].Window.Handle);
        var plan = LayoutPlanner.Plan(Kind, area, chosen.Count);

        var (moved, refused) = _arranger.Apply(
            [.. chosen.Select((group, index) => (group.Window, plan[index]))]);

        Outcome = refused == 0
            ? $"Placed {moved} window(s)."
            : $"Placed {moved} window(s); {refused} would not move. The log says why.";

        Look();
    }

    private void Restore()
    {
        var restored = _arranger.RestoreAll();

        Outcome = restored == 0
            ? "There was nothing to put back."
            : $"Put {restored} window(s) back where they were.";

        Look();
    }
}
