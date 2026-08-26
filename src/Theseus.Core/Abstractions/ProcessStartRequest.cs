namespace Theseus.Core.Abstractions;

/// <summary>Everything needed to start a child process that inherits the caller's console.</summary>
/// <param name="ExecutablePath">The executable to start.</param>
/// <param name="Arguments">
/// The argument vector, passed through <c>ProcessStartInfo.ArgumentList</c> so the runtime owns quoting.
/// </param>
/// <param name="WorkingDirectory">The directory the child starts in.</param>
public sealed record ProcessStartRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);
