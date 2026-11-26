namespace Theseus.Core.Locks;

/// <summary>The installed lock tools as Options shows them.</summary>
public sealed record LockToolsState(IReadOnlyList<LockToolFile> Files)
{
    public bool CanUndo => Files.Any(file => file.CanUndo);

    public string Line =>
        Files.All(file => !file.IsInstalled) ? "Not installed"
        : Files.All(file => file.IsUpToDate) ? "Up to date"
        : $"Out of date: {string.Join(", ", Files.Where(file => !file.IsUpToDate).Select(file => file.Name))}";
}
