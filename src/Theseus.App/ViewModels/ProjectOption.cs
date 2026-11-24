namespace Theseus.App.ViewModels;

/// <summary>A readable project offered by Add to project…, with its objectives as "1.1 Title".</summary>
/// <param name="Name">The project's name.</param>
/// <param name="Title">Its title.</param>
/// <param name="Objectives">Its objectives: number and title.</param>
public sealed record ProjectOption(string Name, string Title, IReadOnlyList<(string Number, string Title)> Objectives)
{
    /// <summary>What a screen reader says for the item: the title the box shows, not the record (W50, G170).</summary>
    public override string ToString() => Title;

    /// <summary>
    /// Which to choose when the window opens: the project it was asked from (1.1e AC 6), else the only one, else none.
    /// </summary>
    public static int IndexToChoose(IReadOnlyList<ProjectOption> projects, string? chosen)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var index = chosen is null
            ? -1
            : projects.ToList().FindIndex(project => string.Equals(project.Name, chosen, StringComparison.OrdinalIgnoreCase));

        return index >= 0 ? index : projects.Count == 1 ? 0 : -1;
    }
}
