using System.Windows;
using System.Windows.Documents;
using Theseus.App.Markdown;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>The one window for a session's proposal (sessions-run-the-project 0.1): its brief drawn, nothing editable.</summary>
public partial class ProposalWindow : Window
{
    /// <summary>Creates the window on its model; it closes when the model says a decision was made or Cancel was pressed.</summary>
    public ProposalWindow(ProposalWindowViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();
        DataContext = model;
        BriefView.Document = MarkdownRenderer.Render(model.Brief ?? $"*The brief cannot be read: {model.Proposal.Request.Brief}*");
        BriefView.AddHandler(Hyperlink.RequestNavigateEvent, new RoutedEventHandler((_, e) =>
        {
            if (e is System.Windows.Navigation.RequestNavigateEventArgs navigate)
            {
                MarkdownRenderer.Follow(navigate.Uri);
                e.Handled = true;
            }
        }));
        model.Closing += (_, _) => Close();
    }
}
