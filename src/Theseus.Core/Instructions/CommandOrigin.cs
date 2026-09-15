namespace Theseus.Core.Instructions;

/// <summary>Where a skill, command or agent was found, which decides whether it wins its name.</summary>
public enum CommandOrigin
{
    /// <summary>The user's own <c>~/.claude</c>.</summary>
    Personal,

    /// <summary>The working directory, or a directory above it within the same repository.</summary>
    Project,

    /// <summary>A <c>.claude</c> below the working directory, loaded once files there are worked on.</summary>
    Nested,

    /// <summary>A directory added to the session.</summary>
    AdditionalDirectory,

    /// <summary>An enabled plugin, under the plugin's own name.</summary>
    Plugin,
}
