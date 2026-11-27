using System.Text.Json;
using System.Text.RegularExpressions;
using Theseus.Core.Persistence;

namespace Theseus.Core.Locks;

/// <summary>
/// The single surface that says what a command line needs locked. The guard reads the same file, and a shared test
/// vector runs through both, so what the guard refuses and what Theseus grants cannot drift apart. Extend it by
/// editing <c>tools/locks/command-classes.json</c> and adding cases to <c>command-classes.tests.json</c>.
/// </summary>
public sealed class CommandClasses
{
    public const string ResourceName = "Theseus.Core.Locks.command-classes.json";

    private static readonly TimeSpan MatchLimit = TimeSpan.FromSeconds(1);
    private static readonly Regex ComposeProject = new(@"(?:\s-p|--project-name)[\s=]+([A-Za-z0-9_.-]+)", RegexOptions.CultureInvariant, MatchLimit);
    private static readonly Regex ComposeProjectVariable = new(@"\bCOMPOSE_PROJECT_NAME=([A-Za-z0-9_.-]+)", RegexOptions.CultureInvariant, MatchLimit);
    private static readonly Regex ComposeFile = new(@"(?:\s-f|--file)[\s=]+(""[^""]+""|'[^']+'|\S+)", RegexOptions.CultureInvariant, MatchLimit);
    private static readonly Regex NotProjectCharacter = new("[^a-z0-9_-]", RegexOptions.CultureInvariant, MatchLimit);

    private readonly IReadOnlyList<Regex> _exempt;
    private readonly IReadOnlyList<(CommandClass Class, Regex Pattern)> _classes;

    public CommandClasses(CommandClassFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        _exempt = [.. file.Exempt.Select(Compile)];
        _classes = [.. file.Classes.Select(commandClass => (commandClass, Compile(commandClass.Pattern)))];
    }

    public static CommandClasses Shipped { get; } = Parse(ShippedText());

    public IReadOnlyList<CommandClass> Classes => [.. _classes.Select(entry => entry.Class)];

    public static string ShippedText()
    {
        using Stream stream = typeof(CommandClasses).Assembly.GetManifestResourceStream(ResourceName)
                              ?? throw new InvalidOperationException($"{ResourceName} is not embedded in Theseus.Core.");
        using StreamReader reader = new(stream);

        return reader.ReadToEnd();
    }

    public static CommandClasses Parse(string json) =>
        new(JsonSerializer.Deserialize<CommandClassFile>(json, TheseusJson.Readable)
            ?? throw new JsonException("command-classes.json is empty."));

    /// <summary>The names of every class the command line belongs to, in the file's order.</summary>
    public IReadOnlyList<string> Classify(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        string text = _exempt.Aggregate(command, (current, exempt) => exempt.Replace(current, "exempt"));

        return [.. _classes.Where(entry => entry.Pattern.IsMatch(text)).Select(entry => entry.Class.Name)];
    }

    /// <summary>
    /// The least a command needs locked: the union of its classes' scopes, with the source tree and compose project
    /// filled in. Empty for a command that needs no lock.
    /// </summary>
    /// <param name="root">The repository's top folder, or the working directory when it is not in one.</param>
    /// <param name="folder">The working directory, which names a compose project as compose does.</param>
    public IReadOnlyList<ResourceClaim> LeastScope(string command, string? root, string? folder = null)
    {
        IReadOnlyList<string> names = Classify(command);
        string tree = string.IsNullOrWhiteSpace(root) ? "unknown" : root.Trim();

        return ResourceClaim.Normalise(_classes
            .Where(entry => names.Contains(entry.Class.Name))
            .SelectMany(entry => entry.Class.Scope)
            .Select(resource => resource
                .Replace("{root}", tree, StringComparison.Ordinal)
                .Replace("{project}", Project(command, folder ?? tree), StringComparison.Ordinal))
            .Select(resource => new ResourceClaim(resource, 1)));
    }

    /// <summary>
    /// The compose project a command uses, as compose names it: <c>-p</c>, then <c>COMPOSE_PROJECT_NAME</c>, then the
    /// folder of the first <c>-f</c> file, then the working folder.
    /// </summary>
    public static string Project(string command, string? folder)
    {
        ArgumentNullException.ThrowIfNull(command);

        string name = ComposeProject.Match(command) is { Success: true } flag
            ? flag.Groups[1].Value
            : ComposeProjectVariable.Match(command) is { Success: true } variable
                ? variable.Groups[1].Value
                : ComposeFile.Match(command) is { Success: true } file && LastFolder(Path.GetDirectoryName(file.Groups[1].Value.Trim('"', '\'')) ?? string.Empty) is { Length: > 0 } fileFolder
                    ? fileFolder
                    : LastFolder(folder ?? string.Empty);

        return NotProjectCharacter.Replace(name.ToLowerInvariant(), string.Empty);
    }

    private static string LastFolder(string path) =>
        Path.GetFileName(path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar));

    private static Regex Compile(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchLimit);
}
