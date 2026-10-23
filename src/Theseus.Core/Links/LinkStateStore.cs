using System.Text.Json;
using System.Text.Json.Nodes;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Links;

/// <summary>
/// <c>links.json</c>: every link, removed ones included, rewritten whole on each change. A file that
/// cannot be read is logged and treated as empty rather than failing the board.
/// </summary>
public sealed class LinkStateStore
{
    /// <summary>Creates a store over a file.</summary>
    public LinkStateStore(string filePath) => FilePath = filePath;

    /// <summary>Where the links are kept.</summary>
    public string FilePath { get; }

    /// <summary>Every link in the file.</summary>
    public IReadOnlyList<Link> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            if (JsonNode.Parse(text) is not JsonArray links)
            {
                return [];
            }

            if (LegacyLinks.Upgrade(links) is var upgraded and > 0)
            {
                TheseusLog.Information("Read {Count} link(s) in {Path} saved as spawned or feeds as hand-overs and report-backs",
                    upgraded, FilePath);
            }

            return links.Deserialize<List<Link>>(TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The links in {Path} could not be read, so the board shows none", FilePath);

            return [];
        }
    }

    /// <summary>Replaces the file with these links.</summary>
    public void Save(IReadOnlyList<Link> links)
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(links, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the links to {Path}", FilePath);
        }
    }
}
