namespace Theseus.Core.Launching;

/// <summary>
/// Mints the session identifier a new Claude Code session is started under. The application chooses
/// the identifier rather than discovering it afterwards, which is what makes a session resumable.
/// </summary>
public interface ISessionIdFactory
{
    /// <summary>Creates a new session identifier.</summary>
    string Create();
}
