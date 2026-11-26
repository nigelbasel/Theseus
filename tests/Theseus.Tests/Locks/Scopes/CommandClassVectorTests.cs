using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

/// <summary>
/// The shared vector (<c>tools/locks/command-classes.tests.json</c>) through Theseus and through the guard, so what
/// the guard refuses and what Theseus grants are the same classes.
/// </summary>
[TestFixture]
public class CommandClassVectorTests
{
    public static IEnumerable<TestCaseData> Cases() =>
        JsonSerializer.Deserialize<List<VectorCase>>(File.ReadAllText(Path.Combine(LocksFolder(), "command-classes.tests.json")),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Select(entry => new TestCaseData(entry.Command, entry.Classes).SetName($"{{m}}({entry.Command})"));

    [TestCaseSource(nameof(Cases))]
    public void Theseus_ClassifiesTheVector(string command, string[] expected)
    {
        Assert.That(CommandClasses.Shipped.Classify(command), Is.EquivalentTo(expected));
    }

    [TestCaseSource(nameof(Cases))]
    public void TheGuard_ClassifiesTheVector(string command, string[] expected)
    {
        string? node = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "node.exe"))
            .FirstOrDefault(File.Exists);

        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        ProcessStartInfo start = new(node!, $"\"{Path.Combine(LocksFolder(), "ndc-test-lock-guard.js")}\" --classify")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(start)!;
        process.StandardInput.Write(command);
        process.StandardInput.Close();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);

        Assert.That(JsonSerializer.Deserialize<string[]>(output), Is.EquivalentTo(expected));
    }

    [Test]
    public void TheShippedFile_IsTheOneTheGuardReads()
    {
        Assert.That(CommandClasses.ShippedText(), Is.EqualTo(File.ReadAllText(Path.Combine(LocksFolder(), "command-classes.json"))));
    }

    [Test]
    public void LeastScope_FillsInTheTreeAndTheComposeProject()
    {
        Assert.That(CommandClasses.Shipped.LeastScope("dotnet build Fabrikam.sln", @"E:\work\x\Fabrikam"),
            Is.EqualTo(new[] { new ResourceClaim("cpu-build", 1), new ResourceClaim("worktree:e:/work/x/fabrikam", 1) }));
        Assert.That(CommandClasses.Shipped.LeastScope("docker compose -p Members-Hub up -d --build", @"E:\work\stack"),
            Is.EqualTo(new[] { new ResourceClaim("compose:members-hub", 1), new ResourceClaim("cpu-build", 1), new ResourceClaim("docker-build", 1) }));
        Assert.That(CommandClasses.Shipped.LeastScope("docker compose build", @"E:\work\Local-Dev"),
            Has.Member(new ResourceClaim("compose:local-dev", 1)));
        Assert.That(CommandClasses.Shipped.LeastScope("dotnet test Fabrikam.Contacts.UnitTests", @"E:\work\x"), Is.Empty);
    }

    private static string LocksFolder([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "..", "tools", "locks"));

    private sealed record VectorCase(string Command, string[] Classes);
}
