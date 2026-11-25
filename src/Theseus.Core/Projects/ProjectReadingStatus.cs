namespace Theseus.Core.Projects;

/// <summary>What became of reading a project file.</summary>
public enum ProjectReadingStatus
{
    /// <summary>Read in the current shape.</summary>
    Readable,

    /// <summary>Its <c>Kit:</c> line names <c>method-v0.1</c>, so it is shown as pinned and not read further.</summary>
    Pinned,

    /// <summary>It could not be read; the reason says where and why. Never shown as empty or guessed.</summary>
    Unreadable,
}
