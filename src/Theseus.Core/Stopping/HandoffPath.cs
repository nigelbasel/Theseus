using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>Decides where a workstream's handoff should be written.</summary>
public static class HandoffPath
{
    /// <summary>
    /// The workstream's own handover document when it has one, and a file beside its working
    /// directory otherwise. A stop request has to name a specific file: told to "write a handoff",
    /// each session would choose its own place and the next morning would be a search.
    /// </summary>
    public static string For(Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        if (!string.IsNullOrWhiteSpace(workstream.StatusDocumentPath))
        {
            return workstream.StatusDocumentPath;
        }

        var directory = string.IsNullOrWhiteSpace(workstream.WorkingDirectory)
            ? Directory.GetCurrentDirectory()
            : workstream.WorkingDirectory;

        return Path.Combine(directory, $"HANDOFF-{workstream.Key}.md");
    }
}
