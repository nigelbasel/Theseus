namespace Theseus.Core.Sessions;

/// <summary>
/// How far a session has got through its own list, in a form short enough for a card. A session
/// reports itself busy or idle, which says whether it is thinking but not what about; this says what.
/// </summary>
/// <param name="Completed">How many items are finished.</param>
/// <param name="Total">How many items there are.</param>
/// <param name="ActiveForm">What the session is doing right now, when it says.</param>
public sealed record TodoProgress(int Completed, int Total, string? ActiveForm)
{
    /// <summary>A session that keeps no list.</summary>
    public static TodoProgress None { get; } = new(0, 0, null);

    /// <summary>Reads one session's list.</summary>
    public static TodoProgress Of(IReadOnlyList<SessionTodo> todos)
    {
        ArgumentNullException.ThrowIfNull(todos);

        if (todos.Count == 0)
        {
            return None;
        }

        return new TodoProgress(
            todos.Count(todo => todo.IsCompleted),
            todos.Count,
            todos.FirstOrDefault(todo => todo.IsInProgress)?.Describe);
    }

    /// <summary>Whether there is anything worth showing.</summary>
    public bool IsKnown => Total > 0;

    /// <summary>The line a card shows.</summary>
    public string Summary => Total switch
    {
        0 => string.Empty,
        _ when !string.IsNullOrWhiteSpace(ActiveForm) => $"{ActiveForm} · {Completed}/{Total}",
        _ when Completed == Total => $"all {Total} done",
        _ => $"{Completed}/{Total} done",
    };
}
