namespace Theseus.Core.Locks;

/// <summary>
/// One kind of command that needs a lock, as <c>tools/locks/command-classes.json</c> writes it for the guard and for
/// Theseus alike. <see cref="Scope"/> may name <c>{root}</c> (the source tree) and <c>{project}</c> (the compose project).
/// </summary>
public sealed record CommandClass(string Name, string Describe, string Pattern, IReadOnlyList<string> Scope);
