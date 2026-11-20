using System.Windows;
using System.Windows.Controls;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>Asks for a new objective's title and criteria (1.1c AC 8). Add stays off until it has a title.</summary>
public partial class AddObjectiveWindow : Window
{
    /// <summary>Creates the window.</summary>
    public AddObjectiveWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TitleBox.Focus();
    }

    /// <summary>What was given, once Add was pressed.</summary>
    public NewObjective Objective =>
        new(TitleBox.Text.Trim(), [.. CriteriaBox.Text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)]);

    private void OnChanged(object sender, TextChangedEventArgs e) => AddButton.IsEnabled = TitleBox.Text.Trim().Length > 0;

    private void OnAdd(object sender, RoutedEventArgs e) => DialogResult = true;
}
