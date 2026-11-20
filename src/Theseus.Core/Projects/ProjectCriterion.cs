namespace Theseus.Core.Projects;

/// <summary>One acceptance criterion under an objective: a <c>- [ ]</c> or <c>- [x]</c> line.</summary>
/// <param name="Text">The criterion, its continuation lines joined with spaces.</param>
/// <param name="IsTicked">Whether it is ticked as met.</param>
/// <param name="Line">Its line in the project file, from 1.</param>
public sealed record ProjectCriterion(string Text, bool IsTicked, int Line);
