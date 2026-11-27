using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.Core.Setup;

/// <summary>
/// When the first-run setup opens by itself, and which steps it shows. Colleagues who installed the MSI found New project
/// greyed out with only a tooltip to say why (Nigel, 2026-11-25: "there should be a startup dialog that runs at startup if
/// the values are not set"), so a board with no projects folder is set up before anything is greyed out.
/// </summary>
public static class FirstRunSetup
{
    /// <summary>
    /// Whether it opens at startup: no projects folder, and the owner has not finished or skipped it before. Only the projects
    /// folder decides, because it is the one setting without which part of the app does not work.
    /// </summary>
    public static bool ShowsAtStartup(TheseusSettings settings) =>
        !settings.FirstRunSetupDone && string.IsNullOrWhiteSpace(settings.ProjectsFolder);

    /// <summary>The steps still missing, in order, with <paramref name="first"/> shown first whether or not it is missing.</summary>
    public static IReadOnlyList<SetupStep> Steps(SetupFacts facts, SetupStep? first = null)
    {
        var missing = new List<SetupStep>();

        if (!facts.HasProjectsFolder)
        {
            missing.Add(SetupStep.Folders);
        }

        if (!facts.HookIsRegistered)
        {
            missing.Add(SetupStep.Hook);
        }

        if (!facts.GuardIsReady)
        {
            missing.Add(SetupStep.Guards);
        }

        if (!facts.FolderTrustIsOn)
        {
            missing.Add(SetupStep.FolderTrust);
        }

        if (facts.Tools.Any(tool => !tool.IsOk))
        {
            missing.Add(SetupStep.Tools);
        }

        if (first is { } asked)
        {
            missing.Remove(asked);
            missing.Insert(0, asked);
        }

        return missing;
    }

    /// <summary>
    /// A warning when the projects folder is inside a code repository, whose records would then need a branch and a pull
    /// request; null otherwise. A folder that is its own repository is the suggested layout, so it is not warned about.
    /// </summary>
    public static string? ProjectsFolderWarning(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || GitRepository.RootOf(folder) is not { } root)
        {
            return null;
        }

        return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)), Path.TrimEndingDirectorySeparator(root),
            StringComparison.OrdinalIgnoreCase)
            ? null
            : $"This folder is inside the code repository at {root}. Records belong outside every code repo.";
    }
}
