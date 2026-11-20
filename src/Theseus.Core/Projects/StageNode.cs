namespace Theseus.Core.Projects;

/// <summary>
/// One stage of a piece of work as the project view shows it (1.1d AC 2): its name, its state in words, its record and
/// what can be done from it. Each kind of stage is its own type, and what it offers is its own override.
/// </summary>
public abstract class StageNode
{
    private protected StageNode(PipelineStage? stage, string name, StageNodeState state, string? reason, string? recordName,
        bool isWritten, bool isSessionState, StageNodeContext context)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);

        Stage = stage;
        Name = name;
        State = state;
        Reason = reason;
        RecordName = recordName;
        RecordFile = isWritten && recordName is not null && context.RecordsFolder is not null
            ? Path.Combine(context.RecordsFolder, recordName)
            : null;
        IsSessionState = isSessionState;
        Context = context;
    }

    /// <summary>The pipeline stage it is, or null for a freeform piece's one Work stage.</summary>
    public PipelineStage? Stage { get; }

    /// <summary>Its name on screen: a bug's stages are Reproduce, Rule and Fix; freeform's is Work.</summary>
    public string Name { get; }

    /// <summary>Where it stands.</summary>
    public StageNodeState State { get; }

    /// <summary>Why it is waiting on you or stuck, in words, or null.</summary>
    public string? Reason { get; }

    /// <summary>The file this stage writes in the records folder, or null when it writes none.</summary>
    public string? RecordName { get; }

    /// <summary>The full path of its record when it is written, or null.</summary>
    public string? RecordFile { get; }

    /// <summary>Whether its state is the card's session lying over the records (AC 5), not the records themselves.</summary>
    public bool IsSessionState { get; }

    /// <summary>What it offers, in the order the view shows them: its own actions, then Open records and its record.</summary>
    public IReadOnlyList<StageActionOffer> Actions
    {
        get
        {
            List<StageActionOffer> actions = [.. OwnActions()];

            actions.Add(Context.RecordsFolder is null
                ? new StageActionOffer(StageAction.OpenRecords, "Open records", false, "No records folder in the project yet")
                : new StageActionOffer(StageAction.OpenRecords, "Open records"));

            if (RecordFile is not null)
            {
                actions.Add(new StageActionOffer(StageAction.OpenRecord, $"Open {RecordName}"));
            }

            return actions;
        }
    }

    /// <summary>The words after "Waiting on you: " or "Stuck: " in the piece's status sentence (AC 7).</summary>
    public virtual string? StatusReason => Reason;

    /// <summary>The piece's records and its card's session.</summary>
    protected StageNodeContext Context { get; }

    /// <summary>The actions only this kind of stage offers.</summary>
    protected abstract IEnumerable<StageActionOffer> OwnActions();
}
