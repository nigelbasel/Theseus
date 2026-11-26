using Theseus.Core.Setup;

namespace Theseus.Tests.Setup;

/// <summary>The setup's tools step: what it finds, reported and never installed (first-run-setup brief step 1).</summary>
[TestFixture]
public class ToolsCheckTests
{
    private static IReadOnlyList<ToolFinding> Run(string? claude, string? node, string? git, bool plugin) =>
        new ToolsCheck(program => program switch
        {
            ToolsCheck.Claude => claude,
            ToolsCheck.Node => node,
            ToolsCheck.Git => git,
            _ => null,
        }, () => plugin).Run();

    [Test]
    public void EverythingInPlace_ReadsAsFourGoodLines()
    {
        var found = Run("2.1.300 (Claude Code)", "v22.11.0", "git version 2.47.1.windows.1", plugin: true);

        Assert.That(found.Select(finding => finding.Name), Is.EqualTo(new[] { "Claude Code", "Node", "git", "agentic-factory plugin" }));
        Assert.That(found.All(finding => finding.IsOk), Is.True, string.Join("; ", found.Select(finding => finding.Line)));
        Assert.That(found[0].Line, Does.Contain("2.1.300"));
    }

    [Test]
    public void AClaudeCodeOlderThanTheMinimum_IsNotOk_AndSaysWhichIsNeeded()
    {
        var claude = Run("2.1.294 (Claude Code)", "v22", "git version 2", true)[0];

        Assert.That(claude.IsOk, Is.False);
        Assert.That(claude.Line, Does.Contain("2.1.294").And.Contain("2.1.295"));
    }

    [Test]
    public void TheMinimumItself_IsOk()
    {
        Assert.That(Run("2.1.295 (Claude Code)", "v22", "git version 2", true)[0].IsOk, Is.True);
    }

    [Test]
    public void AToolThatIsNotFound_IsNotOk_AndSaysSo()
    {
        var found = Run(null, null, null, plugin: false);

        Assert.That(found.Any(finding => finding.IsOk), Is.False);
        Assert.That(found.Select(finding => finding.Line), Has.All.Contain("not"));
    }

    [Test]
    public void AVersionThatCannotBeRead_IsNotOk()
    {
        Assert.That(Run("something odd", "v22", "git version 2", true)[0].IsOk, Is.False);
    }
}
