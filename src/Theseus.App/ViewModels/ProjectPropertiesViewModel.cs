using Theseus.Core.Colour;

namespace Theseus.App.ViewModels;

/// <summary>
/// Project properties… on a project card's menu (1.1d AC 40): its title, which OK writes to its project.md (1.1e AC 12a;
/// Nigel, 2026-11-21, could not rename "Theseus bootstrap"); its colour, chosen from the project palette (decision 7), with
/// the colours other projects hold marked; and the path of its project.md, read only, saying why. Colours, places and the
/// run state stay keyed by the folder's name, so a new title changes nothing else.
/// </summary>
public sealed class ProjectPropertiesViewModel : ObservableObject
{
    private string _colourHex;
    private string _title;
    private string? _error;

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
        ShownTitle = title;
        _title = title;
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

    /// <summary>Its title as the file had it when the dialog opened.</summary>
    public string ShownTitle { get; }

    /// <summary>Its title, as typed: OK writes it to the file when it differs from <see cref="ShownTitle"/>.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
            {
                Error = null;
            }
        }
    }

    /// <summary>Whether the title was changed.</summary>
    public bool TitleChanged => !string.Equals(_title.Trim(), ShownTitle, StringComparison.Ordinal);

    /// <summary>
    /// Writes a changed title, as the shell gives it: the file's refusal, such as the file having changed underneath, or
    /// null once written. Unset, a title is not written.
    /// </summary>
    public Func<string, string?>? WriteTitle { get; init; }

    /// <summary>Why the title was not written, shown under it; null when there is nothing to say.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
            {
                Raise(nameof(HasError));
            }
        }
    }

    /// <summary>Whether a refusal is shown.</summary>
    public bool HasError => _error is not null;

    /// <summary>Why the file's path cannot be edited here.</summary>
    public string FileToolTip => $"{ProjectFile}. Read only: the file is where the project's folder is; move the folder to move it";

    /// <summary>OK: writes a changed title and says whether the dialog may close; a refusal stays, shown under the title.</summary>
    public bool Apply()
    {
        if (!TitleChanged)
        {
            return true;
        }

        if (_title.Trim().Length == 0)
        {
            Error = "A project needs a title.";

            return false;
        }

        Error = WriteTitle?.Invoke(_title.Trim());

        return Error is null;
    }

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
