using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Locks;

/// <summary>
/// <c>resources.json</c> in Theseus's data folder: the resource table, seeded on first use, with every ruling the
/// arbiter or Nigel has made written into it.
/// </summary>
public sealed class ResourceTableStore
{
    public ResourceTableStore(string filePath) => FilePath = filePath;

    public string FilePath { get; }

    public ResourceTable Load()
    {
        string? text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            ResourceTable seeded = ResourceTable.Seeded();
            Save(seeded);

            return seeded;
        }

        try
        {
            List<ResourceRule> rules = JsonSerializer.Deserialize<List<ResourceRule>>(text, TheseusJson.Readable) ?? [];

            return new ResourceTable(rules);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The resource table in {Path} could not be read, so the seeded table is used", FilePath);

            return ResourceTable.Seeded();
        }
    }

    public void Save(ResourceTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(table.Rules, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the resource table to {Path}", FilePath);
        }
    }
}
