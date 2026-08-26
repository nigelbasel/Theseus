namespace Theseus.Core.Terminal;

/// <summary>Which terminal executable a launch will use, and how confident that choice is.</summary>
/// <param name="ExecutablePath">The executable to start, or empty when none was found.</param>
/// <param name="Source">How the executable was chosen.</param>
/// <param name="DistinctRunningPackages">
/// How many different terminal packages are running. More than one means tabs could land in a
/// window the user is not looking at, which the settings screen warns about.
/// </param>
public sealed record TerminalResolution(
    string ExecutablePath,
    TerminalSource Source,
    int DistinctRunningPackages);
