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

    /// <summary>
    /// The <c>wt.exe</c> alias with only one terminal package installed, so it can open no other.
    /// </summary>
    OnlyPackageAlias,

    /// <summary>The <c>wt.exe</c> alias, because Windows refused to start the package's own executable.</summary>
    RefusedFallback,
}
