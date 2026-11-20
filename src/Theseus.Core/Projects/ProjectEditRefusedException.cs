namespace Theseus.Core.Projects;

/// <summary>An edit to a project file that cannot be made as asked; its message says why, for the owner.</summary>
public sealed class ProjectEditRefusedException(string message) : Exception(message);
