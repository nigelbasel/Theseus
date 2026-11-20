using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>spec-acceptances.json</c>: every spec the owner accepted, keyed by project, slug and the accepted text's hash,
/// rewritten whole on each change (spec AC 7). Kept in Theseus's data folder, outside every repository, so a session
/// cannot accept its own spec by committing a file. A file that cannot be read is logged and treated as empty.
/// </summary>
public sealed class SpecAcceptanceStore
{
    /// <summary>Creates a store over a file.</summary>
    public SpecAcceptanceStore(string filePath) => FilePath = filePath;

    /// <summary>Where the acceptances are kept.</summary>
    public string FilePath { get; }

    /// <summary>Every acceptance in the file, oldest first.</summary>
    public IReadOnlyList<SpecAcceptance> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<SpecAcceptance>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The spec acceptances in {Path} could not be read, so every spec shows as waiting", FilePath);

            return [];
        }
    }

    /// <summary>The latest acceptance of a workstream's spec, or null when the owner has accepted none.</summary>
    public SpecAcceptance? Latest(string project, string slug) =>
        Load().LastOrDefault(acceptance => Matches(acceptance, project, slug));

    /// <summary>
    /// Records the owner's acceptance of a spec only when he says yes to the question naming its slug and version
    /// (project-view AC 15); with no one to ask, nothing is accepted.
    /// </summary>
    public bool AcceptIfConfirmed(SpecAtGate spec, Func<string, bool>? confirm, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (confirm?.Invoke(spec.ConfirmationQuestion) != true)
        {
            return false;
        }

        Accept(spec.Accept(now));

        return true;
    }

    /// <summary>Records an acceptance. Accepting the same text again replaces the earlier record of it.</summary>
    public void Accept(SpecAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(acceptance);

        var kept = Load()
            .Where(existing => !(Matches(existing, acceptance.Project, acceptance.Slug)
                                 && string.Equals(existing.ContentHash, acceptance.ContentHash, StringComparison.OrdinalIgnoreCase)))
            .Append(acceptance)
            .ToList();

        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(kept, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the spec acceptances to {Path}", FilePath);

            throw;
        }
    }

    private static bool Matches(SpecAcceptance acceptance, string project, string slug) =>
        string.Equals(acceptance.Project, project, StringComparison.OrdinalIgnoreCase)
        && string.Equals(acceptance.Slug, slug, StringComparison.OrdinalIgnoreCase);
}
