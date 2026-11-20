namespace Theseus.Core.Projects;

/// <summary>
/// Where a piece of work goes next: the first To do stage after its last Done one (1.1d AC 5 and 7). The session lies
/// over that stage, and the status sentence names it.
/// </summary>
internal static class StageOrder
{
    /// <summary>The index of the first To do stage after the last Done one, or -1 when there is none.</summary>
    public static int NextToDo<T>(IReadOnlyList<T> stages, Func<T, StageNodeState> stateOf)
    {
        var lastDone = -1;

        for (var index = 0; index < stages.Count; index++)
        {
            if (stateOf(stages[index]) == StageNodeState.Done)
            {
                lastDone = index;
            }
        }

        for (var index = lastDone + 1; index < stages.Count; index++)
        {
            if (stateOf(stages[index]) == StageNodeState.ToDo)
            {
                return index;
            }
        }

        return -1;
    }
}
