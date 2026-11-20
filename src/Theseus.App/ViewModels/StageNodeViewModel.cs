using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One stage of a piece of work's box (1.1d AC 26 to 30): a chip of the folded strip and a button of the open chain,
/// always with its mark and word so state is never colour alone. Its right-click menu holds Properties, then its own
/// actions.
/// </summary>
public sealed class StageNodeViewModel : ObservableObject
{
    private bool _isSelected;

    internal StageNodeViewModel(StageNode node, int index, Action<StageNodeViewModel> select,
        IReadOnlyList<PieceActionViewModel> actions)
    {
        Node = node;
        IsFirst = index == 0;
        SelectCommand = new RelayCommand(_ => select(this));
        MenuItems = [new PieceActionViewModel("Properties", () => select(this), toolTip: "Show this stage's details", isDefault: true), .. actions];
    }

    /// <summary>The stage.</summary>
    public StageNode Node { get; }

    /// <summary>Its name, such as "Implement", a bug's "Fix" or freeform's "Work".</summary>
    public string Name => Node.Name;

    /// <summary>The first three letters of its name, for the folded strip.</summary>
    public string Abbreviation => Name.Length <= 3 ? Name : Name[..3];

    /// <summary>Where it stands.</summary>
    public StageNodeState State => Node.State;

    /// <summary>The state's mark.</summary>
    public string Mark => StageNodeStates.Mark(State);

    /// <summary>The state's word.</summary>
    public string Word => StageNodeStates.Word(State);

    /// <summary>The chain button's first line: the mark and the stage's name.</summary>
    public string Label => $"{Mark} {Name}";

    /// <summary>"Implement: Done", the chip's and the button's tooltip.</summary>
    public string ToolTip => $"{Name}: {Word}";

    /// <summary>Whether it is the first in its chain, which has no ▶ before it.</summary>
    public bool IsFirst { get; }

    /// <summary>Whether this stage is what the Properties pane shows.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }

    /// <summary>Selects this stage.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>Its right-click menu: Properties (the default), then its actions.</summary>
    public IReadOnlyList<PieceActionViewModel> MenuItems { get; }

    /// <summary>The piece of work it belongs to; set as the box is built.</summary>
    public PieceNodeViewModel Piece { get; internal set; } = null!;
}
