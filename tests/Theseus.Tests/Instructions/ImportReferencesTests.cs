using Theseus.Core.Instructions;

namespace Theseus.Tests.Instructions;

[TestFixture]
public class ImportReferencesTests
{
    [Test]
    public void In_FindsEveryImportOnALine()
    {
        Assert.That(ImportReferences.In("See @README for the overview and @package.json for commands."),
            Is.EqualTo(new[] { "README", "package.json" }));
    }

    [Test]
    public void In_ReadsAbsoluteAndHomeRelativePaths()
    {
        Assert.That(ImportReferences.In("@C:/dev/tfsgit/CLAUDE.md\n- @~/.claude/mine.md"),
            Is.EqualTo(new[] { "C:/dev/tfsgit/CLAUDE.md", "~/.claude/mine.md" }));
    }

    [Test]
    public void In_DoesNotMistakeAnEmailAddressForAnImport()
    {
        Assert.That(ImportReferences.In("Write to nigel@example.com about it."), Is.Empty);
    }

    [Test]
    public void In_SkipsCodeSpansAndFencedBlocks()
    {
        Assert.That(ImportReferences.In("Keep `@literal` as text.\n```text\n@inside\n```\n@after.md"),
            Is.EqualTo(new[] { "after.md" }));
    }

    [Test]
    public void Resolve_ReadsARelativeImportFromBesideTheFileThatHoldsIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"theseus-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "docs"));
        var target = Path.Combine(directory, "docs", "guide.md");
        File.WriteAllText(target, "x");

        try
        {
            Assert.That(ImportReferences.Resolve("docs/guide.md", Path.Combine(directory, "CLAUDE.md"), directory),
                Is.EqualTo(target));
            Assert.That(ImportReferences.Resolve("claude", Path.Combine(directory, "CLAUDE.md"), directory), Is.Null,
                "a mention that names no file is not an import");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
