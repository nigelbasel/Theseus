namespace Theseus.Core.Launching;

/// <inheritdoc />
public sealed class SessionIdFactory : ISessionIdFactory
{
    /// <inheritdoc />
    public string Create() => Guid.NewGuid().ToString("D");
}
