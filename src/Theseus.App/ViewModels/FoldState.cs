namespace Theseus.App.ViewModels;

/// <summary>
/// One fold of the locks panel: a lock, its queue, or a section around them, open or folded, kept by its key in
/// <see cref="LockFolds"/> so it is remembered between runs (Nigel, 2026-11-26).
/// </summary>
public sealed class FoldState : ObservableObject
{
    private bool _isExpanded;

    public FoldState(string key, bool isExpanded)
    {
        Key = key;
        _isExpanded = isExpanded;
    }

    public string Key { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }
}
