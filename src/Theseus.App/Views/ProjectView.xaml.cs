using System.Windows;
using System.Windows.Controls;
using Theseus.App.ViewModels;

namespace Theseus.App.Views;

/// <summary>The project view (project-view AC 14 to 16). Its only code scrolls the pipeline to the tree's selection.</summary>
public partial class ProjectView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ProjectView() => InitializeComponent();

    /// <summary>Scrolls the pipeline to the row of the item selected in the tree (AC 15).</summary>
    public void ScrollTo(ProjectPipelineRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (RowList.ItemContainerGenerator.ContainerFromItem(row) is FrameworkElement container)
        {
            container.BringIntoView();
        }
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ProjectTreeNodeViewModel { Row: { } row })
        {
            ScrollTo(row);
        }
    }
}
