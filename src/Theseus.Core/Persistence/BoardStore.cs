using System.Text.Json;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;

namespace Theseus.Core.Persistence;

/// <inheritdoc />
public sealed class BoardStore : IBoardStore
{
    private readonly string _path;

    /// <summary>Creates a store over a <c>board.json</c> file.</summary>
    public BoardStore(string path) => _path = path;

    /// <inheritdoc />
    public IReadOnlyList<WorkstreamSidecar> Read()
    {
        var text = AtomicFile.TryReadAllText(_path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkstreamSidecar>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Error(exception, "The board file {Path} could not be read. Every card falls back to its defaults", _path);

            return [];
        }
    }

    /// <inheritdoc />
    public void Write(IReadOnlyList<WorkstreamSidecar> sidecars)
    {
        ArgumentNullException.ThrowIfNull(sidecars);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(sidecars, TheseusJson.Readable));
    }
}
