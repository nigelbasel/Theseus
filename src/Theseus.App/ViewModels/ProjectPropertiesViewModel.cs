using Theseus.Core.Colour;

namespace Theseus.App.ViewModels;

/// <summary>
/// Project properties… on a project card's menu (1.1d AC 40): its title, read only; its colour, the one thing changed
/// here, chosen from the project palette (decision 7), with the colours other projects hold marked; and the path of its
/// project.md.
/// </summary>
public sealed class ProjectPropertiesViewModel : ObservableObject
{
    private string _colourHex;

    /// <summary>Creates the properties of a project card.</summary>
    /// <param name="name">The project's name.</param>
    /// <param name="title">Its title.</param>
    /// <param name="colourHex">Its colour now.</param>
    /// <param name="projectFile">The full path of its project.md.</param>
    /// <param name="others">The other projects' colours, by their titles.</param>
    public ProjectPropertiesViewModel(string name, string title, string colourHex, string projectFile,
        IReadOnlyDictionary<string, string>? others = null)
    {
        Name = name;
        Title = title;
        ProjectFile = projectFile;
        _colourHex = colourHex;

        IReadOnlyList<string> colours = ProjectColourPalette.Contains(colourHex)
            ? ProjectColourPalette.All
            : [colourHex, .. ProjectColourPalette.All];

        Colours = [.. colours.Select(colour => new ColourSwatchViewModel(colour, string.Join(", ", (others ?? new Dictionary<string, string>())
            .Where(other => string.Equals(other.Value, colour, StringComparison.OrdinalIgnoreCase))
            .Select(other => other.Key)
            .Order(StringComparer.CurrentCultureIgnoreCase))))];
    }

    /// <summary>The project's name, which keys its colour.</summary>
    public string Name { get; }

    /// <summary>Its title, from its project.md.</summary>
    public string Title { get; }

    /// <summary>The full path of its project.md.</summary>
    public string ProjectFile { get; }

    /// <summary>The colours to choose from: the project palette, and the project's own first when it is not in it.</summary>
    public IReadOnlyList<ColourSwatchViewModel> Colours { get; }

    /// <summary>The colour chosen, as <c>#rrggbb</c>.</summary>
    public string ColourHex
    {
        get => _colourHex;
        set => Set(ref _colourHex, value);
    }
}
