using System.Text.Json;
using System.Text.Json.Serialization;

namespace Theseus.Core.Persistence;

/// <summary>
/// One serialiser configuration for every file the application owns. Enums are written as names
/// and indentation is on, because all of these files are meant to be read, diffed and hand-edited.
/// </summary>
public static class TheseusJson
{
    /// <summary>Settings for files a human or another tool reads.</summary>
    public static readonly JsonSerializerOptions Readable = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Settings for one-object-per-line append-only files.</summary>
    public static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };
}
