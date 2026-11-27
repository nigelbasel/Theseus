namespace Theseus.Core.Launching;

/// <summary>
/// The variables every launched session carries so the lock script can say whose run it is: the Claude session,
/// which owns the run for deadlock detection, and the card, which tells a repeat of the same run.
/// </summary>
public static class SessionEnvironment
{
    public const string SessionVariable = "THESEUS_SESSION_ID";

    public const string CardVariable = "THESEUS_CARD";
}
