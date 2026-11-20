using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>Passes git commands through and remembers each, so a test can say which commands were sent.</summary>
internal sealed class RecordingGit(IGitReader inner) : IGitReader
{
    /// <summary>Every command's arguments, in order.</summary>
    public List<IReadOnlyList<string>> Commands { get; } = [];

    public GitOutput Run(string folder, IReadOnlyList<string> arguments)
    {
        Commands.Add(arguments);

        return inner.Run(folder, arguments);
    }
}
