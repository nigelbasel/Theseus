namespace Theseus.Core.Attention;

/// <summary>Whether a session began work on an issue or stopped working on one.</summary>
public enum WorkMarkKind
{
    /// <summary>The session began work on an issue, ending any it was on before.</summary>
    Start,

    /// <summary>The session stopped working on issues.</summary>
    Stop,
}
