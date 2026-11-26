using Theseus.Core.Settings;

namespace Theseus.Core.Kanban;

/// <summary>
/// The kanban's column widths, collapsed columns and folded rows: one set for the kanban, remembered between runs
/// (projects-kanban spec AC 6, 11, 12, 14). Widths live in <see cref="TheseusSettings.PaneSizes"/>; collapsed columns
/// and folded rows live in <see cref="TheseusSettings.ViewFolds"/>, under keys of their own.
/// </summary>
/// <param name="Widths">Each resized column's width.</param>
/// <param name="Collapsed">The collapsed columns.</param>
/// <param name="FoldedRows">The folded rows, by project name.</param>
public sealed record KanbanLayout(
    IReadOnlyDictionary<KanbanColumn, double> Widths,
    IReadOnlySet<KanbanColumn> Collapsed,
    IReadOnlySet<string> FoldedRows)
{
    /// <summary>The narrowest a column is resized or fitted to.</summary>
    public const double MinWidth = 160;

    /// <summary>The widest.</summary>
    public const double MaxWidth = 560;

    /// <summary>An open column's width before it is resized.</summary>
    public const double DefaultWidth = 240;

    /// <summary>A collapsed column's strip.</summary>
    public const double CollapsedWidth = 36;

    internal const string ColumnsKey = "kanban:columns";

    internal const string RowsKey = "kanban:rows";

    private const string WidthPrefix = "Kanban:";

    /// <summary>Done starts collapsed; every other column starts open at the default width (AC 11).</summary>
    public static KanbanLayout Default { get; } = new(new Dictionary<KanbanColumn, double>(),
        new HashSet<KanbanColumn> { KanbanColumn.Done }, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>A column's width while open, within the bounds.</summary>
    public double WidthOf(KanbanColumn column) => Widths.TryGetValue(column, out var width) ? Clamp(width) : DefaultWidth;

    /// <summary>A width within <see cref="MinWidth"/> and <see cref="MaxWidth"/>.</summary>
    public static double Clamp(double width) => double.IsFinite(width) ? Math.Clamp(width, MinWidth, MaxWidth) : DefaultWidth;

    /// <summary>With one column's width set, clamped.</summary>
    public KanbanLayout WithWidth(KanbanColumn column, double width) =>
        this with { Widths = new Dictionary<KanbanColumn, double>(Widths) { [column] = Clamp(width) } };

    /// <summary>With one column collapsed or opened.</summary>
    public KanbanLayout WithCollapsed(KanbanColumn column, bool collapsed)
    {
        var set = new HashSet<KanbanColumn>(Collapsed);

        if (collapsed)
        {
            set.Add(column);
        }
        else
        {
            set.Remove(column);
        }

        return this with { Collapsed = set };
    }

    /// <summary>With one project's row folded or unfolded.</summary>
    public KanbanLayout WithFolded(string project, bool folded)
    {
        var set = new HashSet<string>(FoldedRows, StringComparer.OrdinalIgnoreCase);

        if (folded)
        {
            set.Add(project);
        }
        else
        {
            set.Remove(project);
        }

        return this with { FoldedRows = set };
    }

    /// <summary>The layout as last left, with <see cref="Default"/> for whatever was never set.</summary>
    public static KanbanLayout From(TheseusSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var widths = new Dictionary<KanbanColumn, double>();

        foreach (var column in KanbanColumns.All)
        {
            if (settings.PaneSizes.TryGetValue(WidthPrefix + column, out var width))
            {
                widths[column] = Clamp(width);
            }
        }

        var collapsed = settings.ViewFolds.TryGetValue(ColumnsKey, out var columns)
            ? columns.Select(name => Enum.TryParse<KanbanColumn>(name, out var column) ? (KanbanColumn?)column : null)
                .OfType<KanbanColumn>().ToHashSet()
            : new HashSet<KanbanColumn>(Default.Collapsed);
        var rows = settings.ViewFolds.TryGetValue(RowsKey, out var folded)
            ? new HashSet<string>(folded, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new KanbanLayout(widths, collapsed, rows);
    }

    /// <summary>The settings with this layout written into them, and nothing else changed.</summary>
    public TheseusSettings Into(TheseusSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var sizes = settings.PaneSizes.Where(pair => !pair.Key.StartsWith(WidthPrefix, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        foreach (var (column, width) in Widths)
        {
            sizes[WidthPrefix + column] = width;
        }

        var folds = new Dictionary<string, IReadOnlyList<string>>(settings.ViewFolds)
        {
            [ColumnsKey] = [.. Collapsed.Order().Select(column => column.ToString())],
            [RowsKey] = [.. FoldedRows.Order(StringComparer.OrdinalIgnoreCase)],
        };

        return settings with { PaneSizes = sizes, ViewFolds = folds };
    }
}
