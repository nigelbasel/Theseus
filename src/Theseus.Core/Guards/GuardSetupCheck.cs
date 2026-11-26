namespace Theseus.Core.Guards;

/// <summary>
/// The machine setup check of the guards: which are missing, out of date, or not run by Claude Code (guards-ship-with-theseus
/// brief step 2, Nigel 2026-11-26). Read only: the board names what it finds, and the first-run setup can show it as a
/// step; nothing is installed except from Options at the owner's click.
/// </summary>
public sealed class GuardSetupCheck
{
    private const string Where = "Tools > Options, Guards";

    private readonly IReadOnlyList<GuardInstallation> _installations;

    /// <summary>The check over some guards' installations.</summary>
    public GuardSetupCheck(IReadOnlyList<GuardInstallation> installations)
    {
        ArgumentNullException.ThrowIfNull(installations);

        _installations = installations;
    }

    /// <summary>The check of every guard Theseus ships, in this user's Claude Code configuration folder.</summary>
    public static GuardSetupCheck ForUser() =>
        new([.. GuardCatalog.All.Select(guard => new GuardInstallation(guard, GuardInstallation.UserConfigFolder()))]);

    /// <summary>Whether every guard is installed, current and run: nothing to set up.</summary>
    public bool IsReady => Findings().Count == 0;

    /// <summary>Each guard not set up, in the order Options shows them; at most one finding a guard, the first to fix.</summary>
    public IReadOnlyList<GuardFinding> Findings() =>
    [
        .. _installations.Select(installation => installation.State())
            .Where(state => !state.IsReady)
            .Select(state => new GuardFinding(state,
                !state.IsInstalled ? GuardProblem.NotInstalled
                : !state.IsUpToDate ? GuardProblem.OutOfDate
                : GuardProblem.NotRun)),
    ];

    /// <summary>
    /// What the board says about the findings, or null: nothing to say, or the same findings were said before. A change in
    /// them, such as an update bringing a newer guard, is said again.
    /// </summary>
    /// <param name="findings">The findings.</param>
    /// <param name="saidFor">The <see cref="Signature"/> of the findings last said, or null.</param>
    public static string? Notice(IReadOnlyList<GuardFinding> findings, string? saidFor)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings.Count == 0 || string.Equals(Signature(findings), saidFor, StringComparison.Ordinal)
            ? null
            : $"Guards to set up in {Where}: {string.Join("; ", findings.Select(finding => finding.Clause))}.";
    }

    /// <summary>What identifies a set of findings, so the same ones are said once: each guard, its problem, and this build's version of it.</summary>
    public static string Signature(IReadOnlyList<GuardFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return string.Join(';', findings.Select(finding =>
            $"{finding.State.Name}:{finding.Problem}:{GuardCatalog.All.FirstOrDefault(guard => guard.Name == finding.State.Name)?.Guard.Version}"));
    }
}
