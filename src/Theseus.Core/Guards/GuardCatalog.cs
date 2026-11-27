using Theseus.Core.Links;

namespace Theseus.Core.Guards;

/// <summary>
/// The guards Theseus ships, so a new machine gets every one from Options without copying anything by hand
/// (guards-ship-with-theseus brief, Nigel 2026-11-26: a lesson lives in Theseus, never only on one machine).
/// </summary>
public static class GuardCatalog
{
    /// <summary>Keeps implementing sessions out of every project's holdout folder.</summary>
    public static GuardDefinition Holdout { get; } = new(
        "holdout guard",
        "Keeps implementing sessions out of every project's holdout folder.",
        new GuardFile("holdout-guard.js", "Theseus.Core.Holdout.holdout-guard.js"),
        [],
        [],
        guard => HoldoutGuardProof.Run(guard));

    /// <summary>Refuses what only the owner does to a trunk and its pull requests, and asks him before a branch is deleted.</summary>
    public static GuardDefinition Trunk { get; } = new(
        "trunk guard",
        "Refuses what only you do to a trunk and its pull requests: merging, approving, abandoning, and pushing to main, master or trunk. Asks you before a branch is deleted or force-pushed.",
        new GuardFile("trunk-guard.js", "Theseus.Core.Guards.trunk-guard.js"),
        [],
        [],
        guard => CommandGuardProof.Run(guard, new CommandGuardCheck(
            "git push origin main", "a push to main",
            "git push origin feature/check", "a push of a workstream branch",
            new Dictionary<string, string>())));

    /// <summary>
    /// Refuses container-backed test runs and solution builds that do not go through the machine-wide lock. The lock
    /// script it sends sessions to, and the command classes it and Theseus both read, are installed beside it. Checked
    /// against an address where no Theseus listens.
    /// </summary>
    public static GuardDefinition TestsLock { get; } = new(
        "tests-lock guard",
        "Refuses container-backed test runs, compose builds and solution builds that do not go through the machine-wide lock, so two sessions never starve each other. Its lock script and the command classes that say what each command needs are installed beside it.",
        new GuardFile("ndc-test-lock-guard.js", "Theseus.Core.Locks.ndc-test-lock-guard.js"),
        [new GuardFile("ndc-test-lock.sh", "Theseus.Core.Locks.ndc-test-lock.sh"), new GuardFile("command-classes.json", "Theseus.Core.Locks.command-classes.json")],
        ["Bash", "PowerShell"],
        guard => CommandGuardProof.Run(guard, new CommandGuardCheck(
            "dotnet test Sample.Api.Tests", "a container-backed test run outside the lock",
            "dotnet test Sample.UnitTests", "a unit test run",
            new Dictionary<string, string> { ["THESEUS_LOCKS_URL"] = "http://localhost:1/locks" })));

    /// <summary>Every guard, in the order Options shows them.</summary>
    public static IReadOnlyList<GuardDefinition> All { get; } = [Holdout, Trunk, TestsLock];
}
