namespace Theseus.Core.Locks;

/// <summary>
/// Decides whether a process is a test host, the only kind of process Theseus will end after a lock
/// holder dies. Build servers (MSBuild nodes, VBCSCompiler, Razor) are shared with other sessions'
/// builds, so a <c>dotnet</c> process counts only when its command line runs a test host.
/// </summary>
public static class TestHostRecogniser
{
    /// <summary>Whether a process with this image name and command line is a test host.</summary>
    public static bool IsTestHost(string imageName, string? commandLine)
    {
        var name = Path.GetFileName(imageName ?? string.Empty);

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        if (name.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("vstest.console", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!name.Equals("dotnet", StringComparison.OrdinalIgnoreCase) || commandLine is null)
        {
            return false;
        }

        return commandLine.Contains("testhost.dll", StringComparison.OrdinalIgnoreCase)
               || commandLine.Contains("vstest.console.dll", StringComparison.OrdinalIgnoreCase);
    }
}
