namespace Theseus.App.ViewModels;

/// <summary>The two links the project view draws as lines (1.1d AC 25); a report back is a mark, not a line.</summary>
public enum ConnectorKind
{
    /// <summary>A card handing work to another: solid, 2 px.</summary>
    HandsOver,

    /// <summary>A workstream moving on to its next piece of work: dotted, 1.5 px.</summary>
    ThenWorkedOn,
}
