using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Theseus.Tests.Repository;

/// <summary>
/// The README is what a new user follows to install Theseus, so what it asks them to install has to match
/// what the app is built for. The published MSI is framework-dependent: a runtime older than the target
/// framework means an app that does not start.
/// </summary>
[TestFixture]
public class ReadmeTests
{
    [Test]
    public void TheRuntimeTheReadmeAsksFor_IsTheOneTheAppTargets()
    {
        var root = RepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "Theseus.App", "Theseus.App.csproj"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        var targeted = Regex.Match(project, @"<TargetFramework>net(\d+)\.0-windows</TargetFramework>").Groups[1].Value;
        var named = Regex.Match(readme, @"\.NET (\d+) Desktop Runtime").Groups[1].Value;
        var linked = Regex.Match(readme, @"dotnet\.microsoft\.com/download/dotnet/(\d+)\.0").Groups[1].Value;

        Assert.That(targeted, Is.Not.Empty, "the App project's target framework");
        Assert.That(named, Is.EqualTo(targeted), "the runtime the README names");
        Assert.That(linked, Is.EqualTo(targeted), "the runtime the README links to");
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
