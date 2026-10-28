using Theseus.Core.Diagnostics;

namespace Theseus.Core.Updates;

/// <summary>Whether a published build should be offered in place of the running one.</summary>
public static class UpdatePolicy
{
    /// <summary>
    /// True when both are stamped builds and the candidate was built later. A local Debug build is
    /// unstamped and is never offered an update: it was built from the source on purpose, and an
    /// installer would replace something else entirely.
    /// </summary>
    public static bool IsNewer(string? current, string? candidate) =>
        BuildStamp.BuiltAt(current) is { } running
        && BuildStamp.BuiltAt(candidate) is { } published
        && published > running;
}
