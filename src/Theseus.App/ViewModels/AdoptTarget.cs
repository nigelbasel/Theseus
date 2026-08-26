using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>One workstream a running session can be adopted into.</summary>
/// <param name="Title">The workstream's title, as shown in the menu.</param>
/// <param name="Command">Performs the adoption. Already carries both the session and the workstream.</param>
public sealed record AdoptTarget(string Title, ICommand Command);
