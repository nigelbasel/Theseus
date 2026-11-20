namespace Theseus.Core.Projects;

/// <summary>
/// The owner's acceptance of a workstream's spec (a bug's rule), recorded on the board in Theseus's own store, never in
/// a file a session can write (spec AC 7).
/// </summary>
/// <param name="Project">The project's name.</param>
/// <param name="Slug">The workstream's slug.</param>
/// <param name="Commit">The commit of the spec he accepted: the last commit that changed it on the branch it was read from.</param>
/// <param name="ContentHash">The accepted text's <see cref="SpecIdentity"/>, which a later spec is compared with.</param>
/// <param name="AcceptedUtc">When he accepted it.</param>
public sealed record SpecAcceptance(string Project, string Slug, string Commit, string ContentHash, DateTimeOffset AcceptedUtc);
