namespace Theseus.Core.Terminal;

/// <summary>Finds the terminal executable that new tabs should be opened with.</summary>
public interface ITerminalResolver
{
    /// <summary>Resolves the terminal to launch into.</summary>
    TerminalResolution Resolve();
}
