using Theseus.Core.Sessions;

namespace Theseus.Core.Abstractions;

/// <summary>Reads the todo list a Claude Code session keeps for itself.</summary>
public interface ISessionTodoReader
{
    /// <summary>The list one session is working through, empty when it keeps none.</summary>
    IReadOnlyList<SessionTodo> Read(string? sessionId);
}
