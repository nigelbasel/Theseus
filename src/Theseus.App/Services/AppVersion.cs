using System.Reflection;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>
/// Which build of Theseus is running.
///
/// Read from the entry assembly rather than passed in, because the question this answers is always
/// asked of the copy in front of you: whether the thing that just started is the thing that was just
/// installed. A number carried in from somewhere else could disagree with the executable and would
/// be worse than none.
/// </summary>
public static class AppVersion
{
    /// <summary>The running build's version, or an empty string if the assembly carries none.</summary>
    public static string Current { get; } = BuildStamp.Format(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString());

    /// <summary>The same, with the build time spelled out.</summary>
    public static string Described { get; } = BuildStamp.Describe(Current);
}
