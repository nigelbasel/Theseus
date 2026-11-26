namespace Theseus.App.ViewModels;

/// <summary>Where the Board / Projects toggle opens the project view: the All projects tab, or a project's tab.</summary>
/// <param name="AllProjects">True for the All projects tab.</param>
/// <param name="Project">The project's name, when it is a project's tab.</param>
public sealed record ViewTarget(bool AllProjects, string? Project);
