using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>start-requests.json</c>: every start request, decided ones included, rewritten whole on each change, so a request
/// survives a restart (sessions-run-the-project item 15). A file that cannot be read is logged and treated as empty.
/// </summary>
public sealed class StartRequestStore
{
    /// <summary>Creates a store over a file.</summary>
    public StartRequestStore(string filePath) => FilePath = filePath;

    /// <summary>Where the requests are kept.</summary>
    public string FilePath { get; }

    /// <summary>Every request in the file.</summary>
    public IReadOnlyList<StartRequest> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StartRequest>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The start requests in {Path} could not be read, so none is shown", FilePath);

            return [];
        }
    }

    /// <summary>Replaces the file with these requests.</summary>
    public void Save(IReadOnlyList<StartRequest> requests)
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(requests, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the start requests to {Path}", FilePath);
        }
    }
}
