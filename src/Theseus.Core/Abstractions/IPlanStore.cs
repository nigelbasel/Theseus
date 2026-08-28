using Theseus.Core.Domain;

namespace Theseus.Core.Abstractions;

/// <summary>
/// Reads and writes the plan files that Claude Code sessions themselves author. A plan that fails
/// validation is reported as absent rather than as a parse error, because the caller's correct
/// response is identical in both cases: keep the last known good version.
/// </summary>
public interface IPlanStore
{
    /// <summary>Every workstream key that has a plan file.</summary>
    IReadOnlyList<string> Keys();

    /// <summary>Where a workstream's plan file lives, whether or not it exists.</summary>
    string PathFor(string key);

    /// <summary>Reads a plan, returning null if it is missing, unreadable or incomplete.</summary>
    SessionPlan? TryRead(string key);

    /// <summary>Writes a plan atomically.</summary>
    void Write(string key, SessionPlan plan);

    /// <summary>Removes a plan file for good. Silent when there was nothing there to remove.</summary>
    void Delete(string key);
}
