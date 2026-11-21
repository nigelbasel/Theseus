namespace Theseus.App.ViewModels;

/// <summary>The way on offered beside a disabled Verify with holdouts (1.1e AC 6a, 6c).</summary>
public enum HoldoutWayOn
{
    /// <summary>Nothing to offer.</summary>
    None,

    /// <summary>Set the holdout folder…: the folder chosen in Options is missing.</summary>
    SetFolder,

    /// <summary>Write scenarios…: the project has none yet, and no card is writing them.</summary>
    WriteScenarios,
}
