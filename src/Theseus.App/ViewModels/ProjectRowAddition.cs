using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>A row Add to project… is to write (1.1c AC 7): into which project, under which objective, and what to say once written.</summary>
/// <param name="Project">The project's name.</param>
/// <param name="Objective">The objective's number.</param>
/// <param name="Row">The workstreams row.</param>
/// <param name="Done">What the status bar says once it is written.</param>
public sealed record ProjectRowAddition(string Project, string Objective, ProjectWorkstreamRow Row, string Done);
