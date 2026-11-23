using System.Text.Json;
using Theseus.Core.Persistence;

namespace Theseus.Core.Links;

/// <summary>
/// Which later reports the owner has opened, by link: the time of the report he opened on each. A box's "Reported again
/// after you confirmed" line shows until he opens that report, and again only for a report filed after it (Nigel,
/// 2026-11-23: "Opening the report clears it").
/// </summary>
public sealed class ReportsOpened
{
    private readonly string _path;
    private Dictionary<string, DateTimeOffset>? _opened;

    /// <summary>Creates the record over its file, read when first asked.</summary>
    public ReportsOpened(string path) => _path = path;

    /// <summary>Whether the owner has opened this report, or one filed after it, on this link.</summary>
    public bool HasOpened(string linkId, DateTimeOffset reportAtUtc) =>
        Opened().TryGetValue(linkId, out var at) && at >= reportAtUtc;

    /// <summary>Records that the owner opened the report filed at this time on this link.</summary>
    public void Open(string linkId, DateTimeOffset reportAtUtc)
    {
        var opened = Opened();

        if (opened.TryGetValue(linkId, out var at) && at >= reportAtUtc)
        {
            return;
        }

        opened[linkId] = reportAtUtc;
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(opened));
    }

    private Dictionary<string, DateTimeOffset> Opened()
    {
        if (_opened is not null)
        {
            return _opened;
        }

        try
        {
            _opened = AtomicFile.TryReadAllText(_path) is { } text
                ? JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(text) ?? []
                : [];
        }
        catch (JsonException)
        {
            _opened = [];
        }

        _opened = new Dictionary<string, DateTimeOffset>(_opened, StringComparer.OrdinalIgnoreCase);

        return _opened;
    }
}
