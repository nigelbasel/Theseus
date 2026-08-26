namespace Theseus.Core.Abstractions;

/// <summary>Finds the Claude Code executable a launch should start.</summary>
public interface IClaudeLocator
{
    /// <summary>The resolved path, or null when Claude Code cannot be found.</summary>
    string? Locate();
}
