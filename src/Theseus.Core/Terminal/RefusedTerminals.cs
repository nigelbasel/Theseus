using System.Collections.Concurrent;

namespace Theseus.Core.Terminal;

/// <summary>
/// Terminal executables Windows has refused to start during this run. Since a Windows update in
/// October 2026 a direct start of the packaged <c>WindowsTerminal.exe</c> under Program
/// Files\WindowsApps can fail with "Access is denied" while the <c>wt.exe</c> alias still works.
/// Once refused, a path is not chosen again until Theseus restarts.
/// </summary>
public sealed class RefusedTerminals
{
    private readonly ConcurrentDictionary<string, byte> _paths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The refusals the board and its settings share.</summary>
    public static RefusedTerminals Shared { get; } = new();

    /// <summary>Whether Windows has refused anything this run.</summary>
    public bool Any => !_paths.IsEmpty;

    /// <summary>Whether Windows has refused this executable.</summary>
    public bool Contains(string path) => _paths.ContainsKey(path);

    /// <summary>Remembers that Windows refused this executable.</summary>
    public void Add(string path) => _paths.TryAdd(path, 0);

    /// <summary>
    /// Whether a failed start is the refusal this works round: access denied, on an executable inside
    /// a package folder. Anything else is a real failure and is reported as one.
    /// </summary>
    public static bool IsPackageRefusal(System.ComponentModel.Win32Exception exception, string path) =>
        exception.NativeErrorCode == 5
        && path.Contains($"{Path.DirectorySeparatorChar}WindowsApps{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(Path.GetFileName(path), "wt.exe", StringComparison.OrdinalIgnoreCase);
}
