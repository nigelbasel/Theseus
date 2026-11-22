using System.Text.Json;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// What sessions tell Theseus through <c>now_at</c> (1.1f E24, E30a). What a card's session is doing is kept in memory
/// until its next call or its card is launched again; a stage a session says it skipped is kept on disk, since the skip
/// stays true after the session ends. Safe to call from the MCP server's threads.
/// </summary>
public sealed class StageReportBook
{
    private readonly string _skipsFile;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, SessionStageReport> _nowAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<StageSkip> _skips;

    /// <summary>Opens the book, reading the skips already said from <paramref name="skipsFile"/>.</summary>
    public StageReportBook(string skipsFile)
    {
        ArgumentNullException.ThrowIfNull(skipsFile);

        _skipsFile = skipsFile;
        _skips = Load(skipsFile);
    }

    /// <summary>Raised after a report or a skip is recorded, on the thread that recorded it.</summary>
    public event EventHandler? Changed;

    /// <summary>The reports as <see cref="PiecesOfWork"/> reads them.</summary>
    public StageReports Reports => new(NowAtOf, SkipsOf);

    /// <summary>Records what a card's session is doing now, replacing what it said before.</summary>
    public void Report(string cardKey, SessionStageReport report)
    {
        lock (_gate)
        {
            _nowAt[cardKey] = report;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records that a piece's session skipped a stage, and why; a second skip of the stage replaces the first.</summary>
    public void Skip(string project, string piece, PipelineStage stage, string reason, DateTimeOffset at)
    {
        lock (_gate)
        {
            _skips.RemoveAll(skip => Same(skip.Project, project) && Same(skip.Piece, piece) && skip.Stage == stage);
            _skips.Add(new StageSkip(project, piece, stage, reason, at));
            AtomicFile.WriteAllText(_skipsFile, JsonSerializer.Serialize(_skips, TheseusJson.Readable));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Forgets what a card's session said, as when the card is launched again and its new session has said nothing.</summary>
    public void Forget(string cardKey)
    {
        lock (_gate)
        {
            _nowAt.Remove(cardKey);
        }
    }

    /// <summary>What a card's session last said, or null.</summary>
    public SessionStageReport? NowAtOf(string cardKey)
    {
        lock (_gate)
        {
            return _nowAt.GetValueOrDefault(cardKey);
        }
    }

    /// <summary>The stages a piece's session said it skipped, with each reason.</summary>
    public IReadOnlyDictionary<PipelineStage, string> SkipsOf(string project, string piece)
    {
        lock (_gate)
        {
            return _skips.Where(skip => Same(skip.Project, project) && Same(skip.Piece, piece))
                .ToDictionary(skip => skip.Stage, skip => skip.Reason);
        }
    }

    private static List<StageSkip> Load(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<List<StageSkip>>(File.ReadAllText(file), TheseusJson.Readable) ?? [] : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            Diagnostics.TheseusLog.Warning(exception, "The stage skips in {File} could not be read; starting with none", file);

            return [];
        }
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
