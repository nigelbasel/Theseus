namespace Theseus.Core.Locks;

/// <summary>The shape of <c>command-classes.json</c>.</summary>
public sealed record CommandClassFile(IReadOnlyList<string> Exempt, IReadOnlyList<CommandClass> Classes);
