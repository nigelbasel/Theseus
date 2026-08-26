namespace Theseus.Core.Sessions;

/// <summary>
/// One item on a session's own todo list, as the session wrote it.
/// </summary>
/// <param name="Id">The item's identifier, which also orders the list.</param>
/// <param name="Subject">What the item is, in the past-neutral form the list shows.</param>
/// <param name="ActiveForm">What the session says it is doing while this item is in progress.</param>
/// <param name="Status">One of <c>pending</c>, <c>in_progress</c> or <c>completed</c>.</param>
public sealed record SessionTodo(string Id, string Subject, string? ActiveForm, string? Status)
{
    /// <summary>Whether the session is working on this item now.</summary>
    public bool IsInProgress => string.Equals(Status, "in_progress", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the item is finished.</summary>
    public bool IsCompleted => string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);

    /// <summary>What to call this item when it is the one in hand.</summary>
    public string Describe => string.IsNullOrWhiteSpace(ActiveForm) ? Subject : ActiveForm;
}
