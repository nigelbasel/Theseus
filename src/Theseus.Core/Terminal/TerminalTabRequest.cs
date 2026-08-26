namespace Theseus.Core.Terminal;

/// <summary>A tab to open in Windows Terminal.</summary>
/// <param name="WindowName">
/// The named window to open the tab in. Adding a tab to an existing named window does not steal
/// foreground; creating the window does.
/// </param>
/// <param name="Title">The tab title.</param>
/// <param name="ColourHex">The tab colour, which can only be set when the tab is created.</param>
/// <param name="WorkingDirectory">The directory the tab starts in.</param>
/// <param name="TargetExecutable">The program the tab runs.</param>
/// <param name="TargetArguments">
/// Arguments for that program. These travel on a command line, so they must stay free of spaces,
/// quotes and semicolons.
/// </param>
public sealed record TerminalTabRequest(
    string WindowName,
    string Title,
    string ColourHex,
    string WorkingDirectory,
    string TargetExecutable,
    IReadOnlyList<string> TargetArguments);
