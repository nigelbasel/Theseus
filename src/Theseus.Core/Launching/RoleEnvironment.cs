using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>
/// The environment a card's role gives its sessions: <c>THESEUS_ROLE</c>, read by the holdout guard (verifier-card D1,
/// Nigel 2026-11-19). Nothing at all for a card with no role.
/// </summary>
public static class RoleEnvironment
{
    /// <summary>The variable the guard reads.</summary>
    public const string Variable = "THESEUS_ROLE";

    /// <summary>What the holdout guard lets through whole: a scenario writer's sessions have it (1.1e AC 6c).</summary>
    public const string HoldoutAuthorVariable = "THESEUS_HOLDOUT_AUTHOR";

    /// <summary>The variables for a role.</summary>
    public static IReadOnlyDictionary<string, string> For(WorkstreamRole role) => role switch
    {
        WorkstreamRole.Verifier => new Dictionary<string, string> { [Variable] = "verifier" },
        WorkstreamRole.ScenarioWriter => new Dictionary<string, string> { [Variable] = "scenario-writer", [HoldoutAuthorVariable] = "1" },
        _ => new Dictionary<string, string>(),
    };
}
