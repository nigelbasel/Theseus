namespace Theseus.Core.Setup;

/// <summary>What is set up on this machine, as the first-run setup reads it when it opens.</summary>
/// <param name="HasProjectsFolder">Whether a projects folder is chosen.</param>
/// <param name="HookIsRegistered">Whether Claude Code's settings run the stop hook.</param>
/// <param name="GuardIsReady">Whether every guard Theseus ships is installed, current, and run by Claude Code.</param>
/// <param name="Tools">What was found of each tool the method needs.</param>
/// <param name="FolderTrustIsOn">Whether the owner's standing folder-trust consent is on.</param>
public sealed record SetupFacts(bool HasProjectsFolder, bool HookIsRegistered, bool GuardIsReady, IReadOnlyList<ToolFinding> Tools,
    bool FolderTrustIsOn = true);
