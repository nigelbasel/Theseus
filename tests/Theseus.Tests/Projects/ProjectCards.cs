using Theseus.Core.Domain;

namespace Theseus.Tests.Projects;

/// <summary>Cards for the membership and layout tests: plain workstreams with only what those rules read.</summary>
internal static class ProjectCards
{
    public static Workstream Card(string key, string? project = null, string? slug = null,
        WorkstreamRole role = WorkstreamRole.None, bool archived = false) =>
        new(key, key, "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, true, archived, null, [], null,
            DateTimeOffset.UnixEpoch)
        {
            Project = project,
            Slug = slug,
            Role = role,
        };
}
