using System.ComponentModel;
using System.Windows;
using Theseus.App.Controls;
using Theseus.App.Markdown;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>A piece's Records window: the document chosen on the left drawn as 1.1e's spec window draws Markdown.</summary>
public partial class RecordsWindow : Window
{
    private readonly RecordsViewModel _model;

    /// <summary>Opens the window over its model.</summary>
    public RecordsWindow(RecordsViewModel model)
    {
        InitializeComponent();
        _model = model ?? throw new ArgumentNullException(nameof(model));
        DataContext = model;
        Draw();
        model.PropertyChanged += OnChanged;
        Loaded += (_, _) => FirstRow.Focus(Entries);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecordsViewModel.Text))
        {
            Draw();
        }
    }

    private void Draw() => Preview.Document = MarkdownRenderer.Render(_model.Text);
}
