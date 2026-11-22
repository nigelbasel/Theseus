using System.Globalization;

namespace Theseus.Core.Links;

/// <summary>The installed holdout guard as Options › Holdouts shows it.</summary>
/// <param name="GuardPath">Where the Claude Code hook runs the guard from.</param>
/// <param name="IsInstalled">Whether a guard is there.</param>
/// <param name="IsUpToDate">Whether it is this Theseus's guard.</param>
/// <param name="InstalledOn">When the installed guard was written, or null when none is.</param>
/// <param name="HookIsRegistered">Whether Claude Code's settings run the guard before a tool.</param>
/// <param name="CanUndo">Whether an earlier copy is kept to put back.</param>
public sealed record HoldoutGuardState(
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

    /// <summary>
    /// What the board says once when an update brings a newer guard than the installed one, or null: nothing installed,
    /// up to date, or already said for this guard (replaces 1.1e's start-up warning).
    /// </summary>
    /// <param name="notifiedVersion">The bundled guard's version the notice was last given for, or null.</param>
    public string? OutOfDateNotice(string? notifiedVersion) =>
        IsInstalled && !IsUpToDate && !string.Equals(notifiedVersion, HoldoutGuardBundle.Version, StringComparison.Ordinal)
            ? "The holdout guard is out of date: install it in Tools > Options, Holdouts."
            : null;
}
