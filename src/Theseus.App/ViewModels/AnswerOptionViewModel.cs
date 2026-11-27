using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>One of a question's own answers, as a button that gives it (2.4e).</summary>
/// <param name="Text">The answer.</param>
/// <param name="Command">Gives it.</param>
public sealed record AnswerOptionViewModel(string Text, ICommand Command);
