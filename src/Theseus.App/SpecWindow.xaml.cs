using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Theseus.App.Markdown;
using Theseus.App.Services;
using Theseus.App.ViewModels;
using Theseus.Core.Layout;
using Theseus.Core.Markdown;

namespace Theseus.App;

/// <summary>
/// The spec window (1.1e AC 24a): resizable, where it was last left, drawn again when its file changes or it comes forward.
/// </summary>
public partial class SpecWindow : Window
{
    private readonly SpecWindowViewModel _model;
    private readonly Action<WindowPlacement>? _remember;
    private readonly WindowPlacement? _placement;
    private readonly DispatcherTimer _settle;
    private FileSystemWatcher? _watcher;

    /// <summary>Creates the window on its model, at the place it was last left.</summary>
    public SpecWindow(SpecWindowViewModel model, WindowPlacement? placement = null, Action<WindowPlacement>? remember = null)
    {
        InitializeComponent();
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _placement = placement;
        _remember = remember;
        DataContext = model;
        PreviewView.AddHandler(Hyperlink.RequestNavigateEvent, new RoutedEventHandler((_, e) =>
        {
            if (e is System.Windows.Navigation.RequestNavigateEventArgs navigate)
            {
                MarkdownRenderer.Follow(navigate.Uri);
                e.Handled = true;
            }
        }));

        // A file written in several saves is read once it has settled.
        _settle = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(400) };
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            _model.Refresh();
        };
        model.PropertyChanged += OnModelChanged;
        model.Redrawn += (_, _) => Draw();
        Draw();
        ShowMode();
        Watch();
        SourceInitialized += (_, _) => Restore();
        Activated += (_, _) => _model.Refresh();
        Closing += OnClosing;
    }

    /// <summary>The model it shows.</summary>
    public SpecWindowViewModel Model => _model;

    /// <summary>
    /// Finds the next match in whichever view is shown and says how many there are, counted in what that view shows; the
    /// focus stays in the search box, so Enter finds the next again (review of PR 5, findings 3 and 9).
    /// </summary>
    public void FindNext()
    {
        var query = _model.Search;

        if (_model.IsSource)
        {
            var count = TextFind.Count(SourceView.Text, query);
            Say(query, count);
            var from = SourceView.SelectionLength > 0 ? SourceView.SelectionStart + 1 : SourceView.SelectionStart;

            if (count > 0 && TextFind.Next(SourceView.Text, query, from) is var at and >= 0)
            {
                SourceView.Select(at, query.Length);
                SourceView.ScrollToLine(SourceView.GetLineIndexFromCharacterIndex(at));
            }

            return;
        }

        if (PreviewView.Document is not { } document)
        {
            return;
        }

        var drawn = new TextRange(document.ContentStart, document.ContentEnd).Text;
        var matches = TextFind.Count(drawn, query);
        Say(query, matches);

        if (matches == 0)
        {
            return;
        }

        _previewFrom = _previewFrom is { } last && last.IsInSameDocument(document.ContentStart) ? last : document.ContentStart;

        if ((FindIn(document, _previewFrom, query) ?? FindIn(document, document.ContentStart, query)) is not { } found)
        {
            FindResult.Text += ", split by formatting";

            return;
        }

        _previewFrom = found.End;
        PreviewView.Selection?.Select(found.Start, found.End);
        (found.Start.Parent as FrameworkContentElement)?.BringIntoView();
    }

    private TextPointer? _previewFrom;

    private void Say(string query, int count) =>
        FindResult.Text = query.Length == 0 ? string.Empty : count == 0 ? "No match" : count == 1 ? "1 match" : $"{count} matches";

    // The next match in the drawn document from a point, run by run; a match never spans two runs.
    private static TextRange? FindIn(FlowDocument document, TextPointer from, string query)
    {
        for (var at = from; at is not null && at.CompareTo(document.ContentEnd) < 0; at = at.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (at.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
            {
                continue;
            }

            var run = at.GetTextInRun(LogicalDirection.Forward);
            var index = run.IndexOf(query, StringComparison.OrdinalIgnoreCase);

            if (index >= 0 && at.GetPositionAtOffset(index) is { } start && start.GetPositionAtOffset(query.Length) is { } end)
            {
                return new TextRange(start, end);
            }
        }

        return null;
    }

    private void Draw()
    {
        _previewFrom = null;
        Title = _model.Title;
        SourceView.Text = _model.Text;
        PreviewView.Document = MarkdownRenderer.Render(_model.Text);
    }

    private void ShowMode()
    {
        PreviewView.Visibility = _model.IsSource ? Visibility.Collapsed : Visibility.Visible;
        SourceView.Visibility = _model.IsSource ? Visibility.Visible : Visibility.Collapsed;
        PreviewTab.FontWeight = _model.IsSource ? FontWeights.Normal : FontWeights.SemiBold;
        SourceTab.FontWeight = _model.IsSource ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpecWindowViewModel.IsSource))
        {
            ShowMode();
        }
    }

    private void Watch()
    {
        if (_model.Shown.FilePath is not { } path || Path.GetDirectoryName(path) is not { } folder || !Directory.Exists(folder))
        {
            return;
        }

        _watcher = new FileSystemWatcher(folder, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        FileSystemEventHandler changed = (_, _) => Dispatcher.BeginInvoke(() =>
        {
            _settle.Stop();
            _settle.Start();
        });
        _watcher.Changed += changed;
        _watcher.Created += changed;
        _watcher.Deleted += changed;
        _watcher.Renamed += (_, _) => Dispatcher.BeginInvoke(_model.Refresh);
    }

    private void Restore()
    {
        if (WindowPlacementFit.Onto(_placement, Displays.WorkAreas()) is { } placement)
        {
            OwnWindowPlacement.Apply(new WindowInteropHelper(this).Handle, placement);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _watcher?.Dispose();
        _settle.Stop();

        if (_remember is not null && OwnWindowPlacement.Capture(new WindowInteropHelper(this).Handle) is { } placement)
        {
            _remember(placement);
        }
    }

    // Ctrl+F and F3 are the window's, wherever the focus is: the preview's own Find would take Ctrl+F otherwise.
    private void OnKey(object sender, KeyEventArgs e) => e.Handled = HandleKey(e.Key, Keyboard.Modifiers);

    /// <summary>Ctrl+F to the search box and F3 for the next match; whether the key was one of them.</summary>
    public bool HandleKey(Key key, ModifierKeys modifiers)
    {
        if (key == Key.F && modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();

            return true;
        }

        if (key == Key.F3 && modifiers == ModifierKeys.None)
        {
            FindNext();

            return true;
        }

        return false;
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
        {
            _model.Search = string.Empty;
            FindResult.Text = string.Empty;
            e.Handled = true;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
