using System.Text.Json;
using Theseus.Core.Persistence;

namespace Theseus.Core.Stopping;

/// <summary>
/// The one line a hook prints to hand a session its request. Kept apart from the decision so the
/// wire format can be pinned by a test that does not care when a delivery happens.
/// </summary>
public static class StopHookReply
{
    /// <summary>The JSON that carries the request into the session as its own user's words.</summary>
    public static string For(string hookEventName, string text) =>
        JsonSerializer.Serialize(
            new
            {
                hookSpecificOutput = new
                {
                    hookEventName,
                    additionalContext = text,
                },
            },
            TheseusJson.Compact);
}
