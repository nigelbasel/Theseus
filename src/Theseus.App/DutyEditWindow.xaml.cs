using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>A duty's dialog: OK saves it and closes, or says under the buttons why it cannot be saved.</summary>
public partial class DutyEditWindow : Window
{
    private readonly DutyEditViewModel _model;

    /// <summary>Creates the dialog over its model.</summary>
    public DutyEditWindow(DutyEditViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        InitializeComponent();
        DataContext = _model = model;
        Loaded += (_, _) => (model.CanRename ? NameBox : InstructionBox).Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (_model.Save())
        {
            DialogResult = true;
        }
    }
}
