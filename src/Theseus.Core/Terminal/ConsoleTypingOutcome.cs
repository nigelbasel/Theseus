namespace Theseus.Core.Terminal;

/// <summary>What became of typing a line into a session's console.</summary>
public enum ConsoleTypingOutcome
{
    /// <summary>Every key event was written into the console's input buffer.</summary>
    Sent,

    /// <summary>The session's console could not be joined: it has ended, runs elevated, or has no console.</summary>
    NotAttached,

    /// <summary>The console was joined but its input could not be opened.</summary>
    NoInput,

    /// <summary>The input was opened but not every key event was written; part of the line may have been typed.</summary>
    PartlyWritten,
}
