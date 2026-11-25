namespace Theseus.Core.Projects;

/// <summary>What <see cref="ProjectFolderWatcher.Watch"/> found.</summary>
public enum ProjectFolderWatch
{
    /// <summary>It cannot watch: no folder, or the folder is not there.</summary>
    NotWatching,

    /// <summary>It started watching just now, so nothing seen before can be trusted to be current.</summary>
    Started,

    /// <summary>It was already watching the folder, and the folder is still there.</summary>
    Watching,
}
