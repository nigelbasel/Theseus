using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>A row of the Records panel in Properties: a click opens the Records window on its document (Nigel, 2026-11-22).</summary>
public sealed class RecordRowViewModel
{
    internal RecordRowViewModel(RecordEntry entry, ICommand open)
    {
        Entry = entry;
        OpenCommand = open;
    }

    /// <summary>The row.</summary>
    public RecordEntry Entry { get; }

    /// <summary>"Spec: what will be built, for you to accept".</summary>
    public string Text => Entry.IsOther ? Entry.Stage : $"{Entry.Stage}: {Entry.Purpose}";

    /// <summary>Its state in words.</summary>
    public string StateText => Entry.StateText;

    /// <summary>The file's name, small and muted.</summary>
    public string FileName => Entry.FileName;

    /// <summary>Opens the Records window on it.</summary>
    public ICommand OpenCommand { get; }
}
