namespace Theseus.Core.Guards;

/// <summary>
/// What the owner enters once for auto mode's environment, kept in his own Theseus settings and never in the repository
/// (guards-ship-with-theseus brief step 3, Nigel 2026-11-26).
/// </summary>
/// <param name="SourceControlHosts">The source-control hosts he pushes to, such as git.example.com or github.com/someone.</param>
/// <param name="InternalDomains">The domains trusted by name; any other is outside the trust boundary.</param>
/// <param name="ProductionPatterns">Words that mark a host, namespace or resource as production, such as prod or live.</param>
public sealed record AutoModeEntries(
    IReadOnlyList<string> SourceControlHosts,
    IReadOnlyList<string> InternalDomains,
    IReadOnlyList<string> ProductionPatterns);
