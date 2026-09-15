namespace Theseus.Core.Instructions;

/// <summary>A plugin that is installed and switched on.</summary>
/// <param name="Name">The plugin's name, which prefixes everything it provides.</param>
/// <param name="InstallPath">Where its files are.</param>
public sealed record InstalledPlugin(string Name, string InstallPath);
