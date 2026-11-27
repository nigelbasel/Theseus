using System.Windows.Input;
using Theseus.Core.Questions;

namespace Theseus.App.ViewModels;

/// <summary>
/// A session's question, answered in Theseus (2.4e, Nigel 2026-11-26: "there is nothing to answer in those sessions"):
/// the question, why it is asked, each option as a button, and an answer in the owner's own words. Go to its tab stays for
/// a session that has one.
/// </summary>
public sealed class AnswerQuestionViewModel : ObservableObject
{
    private string _ownWords = string.Empty;

    /// <summary>Creates it for a card's open question.</summary>
    /// <param name="cardTitle">The card that asks.</param>
    /// <param name="question">Its question.</param>
    /// <param name="hasTab">Whether its session is open in a tab, so Go to its tab is offered.</param>
    /// <param name="reachesWhen">When the answer reaches it, in plain words, said under the answer.</param>
    public AnswerQuestionViewModel(string cardTitle, OwnerQuestion question, bool hasTab, string reachesWhen)
    {
        ArgumentNullException.ThrowIfNull(question);
        CardTitle = cardTitle;
        Question = question.Text;
        Why = string.IsNullOrWhiteSpace(question.Why) ? null : question.Why;
        Options = [.. question.Options.Select(option => new AnswerOptionViewModel(option, new RelayCommand(_ => Choose(option))))];
        HasTab = hasTab;
        ReachesWhen = reachesWhen;
        SendCommand = new RelayCommand(_ => Choose(OwnWords.Trim()), _ => OwnWords.Trim().Length > 0);
        GoToTabCommand = new RelayCommand(_ => Finish(goToTab: true), _ => HasTab);
    }

    /// <summary>Raised when an answer is chosen or Go to its tab is pressed, so the window closes.</summary>
    public event EventHandler? Finished;

    /// <summary>The window's title.</summary>
    public string Heading => $"{CardTitle} asks";

    /// <summary>The card that asks.</summary>
    public string CardTitle { get; }

    /// <summary>The question.</summary>
    public string Question { get; }

    /// <summary>Why it is asked, or null.</summary>
    public string? Why { get; }

    /// <summary>Whether there is a why to show.</summary>
    public bool HasWhy => Why is not null;

    /// <summary>The session's own answers, each a button that gives it.</summary>
    public IReadOnlyList<AnswerOptionViewModel> Options { get; }

    /// <summary>Whether it offers answers to choose from.</summary>
    public bool HasOptions => Options.Count > 0;

    /// <summary>Whether its session is open in a tab.</summary>
    public bool HasTab { get; }

    /// <summary>When the answer reaches the session.</summary>
    public string ReachesWhen { get; }

    /// <summary>An answer in the owner's own words.</summary>
    public string OwnWords
    {
        get => _ownWords;
        set => Set(ref _ownWords, value);
    }

    /// <summary>Gives the answer in his own words; off while it is empty.</summary>
    public ICommand SendCommand { get; }

    /// <summary>Brings its tab forward instead, answering nothing.</summary>
    public ICommand GoToTabCommand { get; }

    /// <summary>The answer chosen, or null.</summary>
    public string? Answer { get; private set; }

    /// <summary>Whether Go to its tab was pressed.</summary>
    public bool GoToTab { get; private set; }

    private void Choose(string answer)
    {
        if (answer.Length == 0)
        {
            return;
        }

        Answer = answer;
        Finish(goToTab: false);
    }

    private void Finish(bool goToTab)
    {
        GoToTab = goToTab;
        Finished?.Invoke(this, EventArgs.Empty);
    }
}
