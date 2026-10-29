using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Proposals;

/// <summary>
/// <c>proposals.json</c>: every proposal, decided ones included, rewritten whole on each change. A file
/// that cannot be read is logged and treated as empty rather than failing the board.
/// </summary>
public sealed class ProposalStateStore
{
    /// <summary>Creates a store over a file.</summary>
    public ProposalStateStore(string filePath) => FilePath = filePath;

    /// <summary>Where the proposals are kept.</summary>
    public string FilePath { get; }

    /// <summary>Every proposal in the file.</summary>
    public IReadOnlyList<WorkstreamProposal> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkstreamProposal>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The proposals in {Path} could not be read, so the board shows none", FilePath);

            return [];
        }
    }

    /// <summary>Replaces the file with these proposals.</summary>
    public void Save(IReadOnlyList<WorkstreamProposal> proposals)
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(proposals, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the proposals to {Path}", FilePath);
        }
    }
}
