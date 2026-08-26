namespace Theseus.Core.Terminal;

/// <summary>Where the terminal executable a launch will use came from.</summary>
public enum TerminalSource
{
    /// <summary>No terminal could be found at all.</summary>
    None,

    /// <summary>The executable hosting the terminal windows already open on this machine.</summary>
    RunningInstance,

    /// <summary>The <c>wt.exe</c> app execution alias, whichever package currently owns it.</summary>
    ExecutionAlias,
}
