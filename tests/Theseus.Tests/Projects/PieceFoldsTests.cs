using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>The stored folds of a project view (1.1d AC 33): own entry, else *, else folded.</summary>
[TestFixture]
public class PieceFoldsTests
{
    [Test]
    public void TheKey_IsPiecesProjectAndTheProjectsName()
    {
        Assert.That(PieceFolds.KeyFor("theseus"), Is.EqualTo("pieces:Project:theseus"));
    }

    [Test]
    public void ABox_IsFoldedWithNoEntry()
    {
        Assert.That(PieceFolds.IsOpen([], "a-piece"), Is.False);
    }

    [Test]
    public void ABox_TakesItsOwnEntry_BeforeStar()
    {
        IReadOnlyList<string> entries = ["*=open", "a-piece=folded"];

        Assert.That(PieceFolds.IsOpen(entries, "a-piece"), Is.False);
        Assert.That(PieceFolds.IsOpen(entries, "another"), Is.True);
    }

    [Test]
    public void With_ReplacesTheBoxsEntry_AndKeepsTheRest()
    {
        IReadOnlyList<string> entries = ["*=folded", "a-piece=open", "b-piece=open"];

        Assert.That(PieceFolds.With(entries, "a-piece", false), Is.EqualTo(new[] { "*=folded", "b-piece=open", "a-piece=folded" }));
    }

    [Test]
    public void All_IsOneStarEntry()
    {
        Assert.That(PieceFolds.All(true), Is.EqualTo(new[] { "*=open" }));
        Assert.That(PieceFolds.All(false), Is.EqualTo(new[] { "*=folded" }));
    }

    [Test]
    public void Each_WritesEveryBox()
    {
        Assert.That(PieceFolds.Each([("a", true), ("b", false)]), Is.EqualTo(new[] { "a=open", "b=folded" }));
    }

    [Test]
    public void APieceNamedWithAnEqualsSign_StillReads()
    {
        Assert.That(PieceFolds.IsOpen(PieceFolds.With([], "odd=name", true), "odd=name"), Is.True);
    }
}
