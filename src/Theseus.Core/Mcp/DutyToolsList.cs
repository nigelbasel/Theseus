using System.Text.Json.Nodes;

namespace Theseus.Core.Mcp;

/// <summary>Adds the duty tools to the server's tools/list answer.</summary>
public static class DutyToolsList
{
    /// <summary>The answer, with <see cref="DutyTools.Definitions"/> appended to its tools.</summary>
    public static JsonObject WithDutyTools(this JsonObject list)
    {
        ArgumentNullException.ThrowIfNull(list);

        var tools = (JsonArray)list["tools"]!;

        foreach (var definition in DutyTools.Definitions())
        {
            tools.Add(definition);
        }

        return list;
    }
}
