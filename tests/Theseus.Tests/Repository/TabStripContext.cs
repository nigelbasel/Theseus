using System.Windows.Input;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Repository;

/// <summary>
/// What the tab strip binds to in the shell, standing in for it in tests: the tabs, the open one as the selection (selecting
/// another runs its open command, as the shell does), and Back and Forward.
/// </summary>
internal sealed class TabStripContext(IReadOnlyList<ProjectTabViewModel> tabs)
{
    /// <summary>The tabs.</summary>
    public IReadOnlyList<ProjectTabViewModel> ProjectTabs { get; } = tabs;

    /// <summary>The open tab; setting another runs its open command.</summary>
    public ProjectTabViewModel? OpenProjectTab
    {
        get => ProjectTabs.FirstOrDefault(tab => tab.IsOpen);
        set
        {
            if (value is { IsOpen: false, OpenCommand: { } open } && open.CanExecute(null))
            {
                open.Execute(null);
            }
        }
    }

    /// <summary>Back.</summary>
    public ICommand? GoBackCommand { get; init; }

    /// <summary>Forward.</summary>
    public ICommand? GoForwardCommand { get; init; }
}
