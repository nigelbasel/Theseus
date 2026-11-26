namespace Theseus.Core.Setup;

/// <summary>A step of the first-run setup, in the order the steps are shown (first-run-setup brief step 1, Nigel 2026-11-25).</summary>
public enum SetupStep
{
    /// <summary>The projects, worktrees and clones folders.</summary>
    Folders,

    /// <summary>The stop hook in Claude Code's settings.</summary>
    Hook,

    /// <summary>The guards Theseus ships (holdout, trunk, tests-lock), each installed, current and run by Claude Code.</summary>
    Guards,

    /// <summary>Claude Code, Node, git and the agentic-factory plugin: reported, never installed.</summary>
    Tools,
}
