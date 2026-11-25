namespace Theseus.Core.Links;

/// <summary>How a project's holdout folder was found (1.1e AC 6e), as Options says it.</summary>
public enum HoldoutSource
{
    /// <summary>A folder the owner chose for this project in its Project properties.</summary>
    Chosen,

    /// <summary>The clone of the holdout repository the project's project.md records (options-made-clear item 4).</summary>
    Recorded,

    /// <summary>A <c>&lt;repo&gt;-holdout</c> folder beside the clone of the project's home code repo.</summary>
    BesideRepo,

    /// <summary>Theseus's own folder under its local data, git-initialised on first use.</summary>
    TheseusOwn,
}
