using System.IO;
using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The Records window of a piece of work (Nigel, 2026-11-22): its documents one row per stage, each with
/// its purpose in plain words and its state, and the chosen one shown formatted. The folder itself is a small link at the
/// foot, one click further: Theseus's own screen first, the raw file second.
/// </summary>
public sealed class RecordsViewModel : ObservableObject
{
    private RecordEntry? _selected;
    private string _text = string.Empty;
    private bool _isSource;

    /// <summary>Opens the records of a piece, the given stage's document chosen when there is one.</summary>
    public RecordsViewModel(PieceOfWork piece, string? stage, Action<string> openPath)
    {
        ArgumentNullException.ThrowIfNull(piece);
        ArgumentNullException.ThrowIfNull(openPath);

        Title = $"Records of {piece.Title}";
        Entries = PieceRecords.For(piece);
        NoneText = PieceRecords.NoneText(piece);
        var folder = piece.RecordsFolder;
        OpenFolderCommand = new RelayCommand(_ => openPath(folder!), _ => folder is not null && Directory.Exists(folder));
        OpenInEditorCommand = new RelayCommand(_ => openPath(_selected!.File!), _ => _selected?.HasFile == true);
        Selected = Entries.FirstOrDefault(entry => entry.Stage == stage && entry.HasFile) ?? Entries.FirstOrDefault(entry => entry.HasFile);
    }

    /// <summary>"Records of &lt;piece title&gt;".</summary>
    public string Title { get; }

    /// <summary>Its rows, in stage order.</summary>
    public IReadOnlyList<RecordEntry> Entries { get; }

    /// <summary>What it says when there is nothing to show, or null.</summary>
    public string? NoneText { get; }

    /// <summary>Whether it has something to say instead of documents.</summary>
    public bool HasNone => NoneText is not null;

    /// <summary>The row whose document is shown.</summary>
    public RecordEntry? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                Text = Read(value);
            }
        }
    }

    /// <summary>The shown document's Markdown, rendered by the window.</summary>
    public string Text
    {
        get => _text;
        private set => Set(ref _text, value);
    }

    /// <summary>Whether the document shows as its Markdown source rather than drawn.</summary>
    public bool IsSource
    {
        get => _isSource;
        set
        {
            if (Set(ref _isSource, value))
            {
                Raise(nameof(IsPreview));
            }
        }
    }

    /// <summary>Whether it is drawn.</summary>
    public bool IsPreview => !_isSource;

    /// <summary>Shows it drawn.</summary>
    public ICommand PreviewCommand => new RelayCommand(_ => IsSource = false);

    /// <summary>Shows its Markdown.</summary>
    public ICommand SourceCommand => new RelayCommand(_ => IsSource = true);

    /// <summary>The small link at the foot: the folder in Explorer.</summary>
    public ICommand OpenFolderCommand { get; }

    /// <summary>The shown document in the editor.</summary>
    public ICommand OpenInEditorCommand { get; }

    private static string Read(RecordEntry? entry)
    {
        if (entry?.File is not { } file)
        {
            return entry is null ? string.Empty : $"{entry.Stage}: nothing written yet.";
        }

        try
        {
            return File.ReadAllText(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"{entry.Stage}: its document could not be read ({exception.Message}).";
        }
    }
}
