namespace Theseus.Core.Instructions;

/// <summary>Where an instruction file sits in the order Claude Code reads them.</summary>
public enum InstructionScope
{
    /// <summary>Set for everyone on the machine, and read first.</summary>
    Managed,

    /// <summary>The user's own CLAUDE.md, for every project.</summary>
    User,

    /// <summary>A rule in the user's own rules folder, for every project.</summary>
    UserRule,

    /// <summary>A CLAUDE.md in a directory above the working directory.</summary>
    Ancestor,

    /// <summary>A CLAUDE.md in the working directory itself.</summary>
    Project,

    /// <summary>A CLAUDE.local.md, read straight after the CLAUDE.md beside it.</summary>
    Local,

    /// <summary>A rule in the working directory's rules folder.</summary>
    ProjectRule,

    /// <summary>A file another one imports with <c>@path</c>.</summary>
    Import,

    /// <summary>A CLAUDE.md below the working directory, read only when files there are.</summary>
    Subdirectory,

    /// <summary>A CLAUDE.md in a directory added to the session.</summary>
    AdditionalDirectory,
}
