using System.Collections.ObjectModel;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>The Sessions panel's members as MainWindow binds them: the live sessions, none unless a test adds them.</summary>
internal sealed class FakeSessions
{
    /// <summary>The live sessions the panel lists.</summary>
    public ObservableCollection<LiveSessionViewModel> Sessions { get; } = [];

    /// <summary>The panel's status line.</summary>
    public string Status { get; set; } = string.Empty;
}
