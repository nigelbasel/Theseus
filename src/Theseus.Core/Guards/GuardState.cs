using System.Globalization;

namespace Theseus.Core.Guards;

/// <summary>An installed guard as Options, Guards shows it.</summary>
/// <param name="Name">The guard's name, such as "trunk guard".</param>
/// <param name="GuardPath">Where the Claude Code hook runs the guard from.</param>
/// <param name="IsInstalled">Whether a guard is there.</param>
/// <param name="IsUpToDate">Whether it, and every file installed beside it, is this Theseus's.</param>
/// <param name="InstalledOn">When the installed guard was written, or null when none is.</param>
/// <param name="HookIsRegistered">Whether Claude Code's settings run the guard before the tools it must see.</param>
/// <param name="CanUndo">Whether an earlier copy is kept to put back.</param>
public sealed record GuardState(
    string Name,
    string GuardPath,
    bool IsInstalled,
    bool IsUpToDate,
    DateTime? InstalledOn,
    bool HookIsRegistered,
    bool CanUndo)
{
    /// <summary>One line on the guard: not installed, up to date, or when the older one was installed.</summary>
    public string Line =>
        !IsInstalled ? "Not installed"
        : IsUpToDate ? "Up to date"
        : InstalledOn is { } on
            ? $"Installed on {on.ToString("d MMMM", CultureInfo.InvariantCulture)}; this Theseus has a newer one"
            : "Installed; this Theseus has a newer one";

    /// <summary>Whether it is installed, this Theseus's own, and run by Claude Code: nothing is left to set up.</summary>
    public bool IsReady => IsInstalled && IsUpToDate && HookIsRegistered;
}
