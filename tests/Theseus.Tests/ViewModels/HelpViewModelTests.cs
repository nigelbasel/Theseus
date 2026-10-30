using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>Help is the published pages; F1 opens the contents, a panel's "?" its own page.</summary>
[TestFixture]
public class HelpViewModelTests
{
    [TestCase(null, "https://nigelbasel.github.io/Theseus/")]
    [TestCase("locks", "https://nigelbasel.github.io/Theseus/locks.html")]
    [TestCase("board#folding", "https://nigelbasel.github.io/Theseus/board.html#folding")]
    public void PageUrl_NamesThePublishedPage(string? page, string url)
    {
        Assert.That(HelpViewModel.PageUrl(page), Is.EqualTo(url));
    }

    [Test]
    public void EveryPageTheAppLinksToIsInTheDocsFolder()
    {
        var docs = Docs();

        foreach (var page in new[] { "index", "board", "cards", "starting-and-stopping", "chains", "locks", "updates", "shortcuts" })
        {
            Assert.That(File.Exists(Path.Combine(docs, $"{page}.md")), Is.True, $"docs/{page}.md");
        }
    }

    private static string Docs([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "docs"));
}
