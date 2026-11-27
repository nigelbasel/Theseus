using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>A session's question answered in Theseus: each option a button, or the owner's own words (2.4e).</summary>
public partial class AnswerQuestionWindow : Window
{
    /// <summary>Creates the window over the question.</summary>
    public AnswerQuestionWindow(AnswerQuestionViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        InitializeComponent();
        DataContext = model;
        model.Finished += (_, _) => QuestionWindow.Finish(this, true);
        Loaded += (_, _) => OwnWords.Focus();
    }
}
