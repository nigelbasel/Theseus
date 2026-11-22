using Theseus.Core.Markdown;

namespace Theseus.Tests.Markdown;

/// <summary>The preview's reading of each element (1.1e AC 23, 25).</summary>
[TestFixture]
public class MarkdownParserTests
{
    [Test]
    public void EachBlock_IsRead_AsItsKind()
    {
        const string text = """
            # Title
            ## Section ##

            Some text
            carried on.

            - one
              more of one
            - two
              - nested
            1. first
            7) seventh
            - [ ] open
            - [x] done

            > quoted
            > still quoted

            ```csharp
            var x = 1;

            # not a heading
            ```

            | A | B |
            |---|:-:|
            | 1 | 2 \| 3 |

            ---
            """;

        var blocks = MarkdownParser.Blocks(text);

        Assert.That(blocks.Select(block => block.Kind), Is.EqualTo(new[]
        {
            MarkdownBlockKind.Heading, MarkdownBlockKind.Heading, MarkdownBlockKind.Paragraph, MarkdownBlockKind.Bullet,
            MarkdownBlockKind.Bullet, MarkdownBlockKind.Bullet, MarkdownBlockKind.Numbered, MarkdownBlockKind.Numbered,
            MarkdownBlockKind.Task, MarkdownBlockKind.Task, MarkdownBlockKind.Quote, MarkdownBlockKind.Code, MarkdownBlockKind.Table,
            MarkdownBlockKind.Rule,
        }));
        Assert.That((blocks[0].Level, blocks[0].Text, blocks[1].Level, blocks[1].Text), Is.EqualTo((1, "Title", 2, "Section")));
        Assert.That(blocks[2].Text, Is.EqualTo("Some text carried on."));
        Assert.That(blocks[3].Text, Is.EqualTo("one more of one"), "an indented line carries on its item");
        Assert.That((blocks[5].Text, blocks[5].Depth), Is.EqualTo(("nested", 1)));
        Assert.That((blocks[6].Number, blocks[7].Number), Is.EqualTo((1, 7)));
        Assert.That((blocks[8].IsChecked, blocks[9].IsChecked, blocks[9].Text), Is.EqualTo((false, true, "done")));
        Assert.That(blocks[10].Text, Is.EqualTo("quoted still quoted"));
        Assert.That(blocks[11].Text, Is.EqualTo("var x = 1;\n\n# not a heading"), "a code block is kept as typed");
        Assert.That(blocks[12].Rows, Is.EqualTo(new[] { new[] { "A", "B" }, new[] { "1", "2 | 3" } }));
    }

    [Test]
    public void EachInline_IsRead_AsItsKind()
    {
        var spans = MarkdownParser.Inlines(@"Plain **bold** and *it* and _it_ with `code`, [a link](https://example.com/x) and https://example.org. Not\*em\* nor snake_case_name");

        Assert.That(spans.Select(span => (span.Kind, span.Text, span.Url)), Is.EqualTo(new (MarkdownInlineKind, string, string?)[]
        {
            (MarkdownInlineKind.Text, "Plain ", null),
            (MarkdownInlineKind.Strong, "bold", null),
            (MarkdownInlineKind.Text, " and ", null),
            (MarkdownInlineKind.Emphasis, "it", null),
            (MarkdownInlineKind.Text, " and ", null),
            (MarkdownInlineKind.Emphasis, "it", null),
            (MarkdownInlineKind.Text, " with ", null),
            (MarkdownInlineKind.Code, "code", null),
            (MarkdownInlineKind.Text, ", ", null),
            (MarkdownInlineKind.Link, "a link", "https://example.com/x"),
            (MarkdownInlineKind.Text, " and ", null),
            (MarkdownInlineKind.Link, "https://example.org", "https://example.org"),
            (MarkdownInlineKind.Text, ". Not*em* nor snake_case_name", null),
        }));
    }

    [Test]
    public void AFenceIndentedUnderAnItem_IsACodeBlock_NotTheItemsText()
    {
        var blocks = MarkdownParser.Blocks("1. Run:\n   ```bash\n   dotnet test\n   ```\n2. Then");

        Assert.That(blocks.Select(block => (block.Kind, block.Text)), Is.EqualTo(new[]
        {
            (MarkdownBlockKind.Numbered, "Run:"), (MarkdownBlockKind.Code, "   dotnet test"), (MarkdownBlockKind.Numbered, "Then"),
        }));
    }

    [TestCase("*a * b", MarkdownInlineKind.Text)]
    [TestCase("_a_b", MarkdownInlineKind.Text)]
    public void AnEmphasisCloser_MustTouchItsWord(string text, MarkdownInlineKind first)
    {
        Assert.That(MarkdownParser.Inlines(text)[0].Kind, Is.EqualTo(first));
    }

    [Test]
    public void ALongText_IsReadQuickly()
    {
        var text = string.Concat(Enumerable.Repeat("hello [there and http words ", 5000));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        MarkdownParser.Inlines(text);

        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)), "the scan is not quadratic");
    }

    [Test]
    public void NothingAtAll_IsNoBlocks()
    {
        Assert.That((MarkdownParser.Blocks(null).Count, MarkdownParser.Blocks("\n\n").Count, MarkdownParser.Inlines(null).Count), Is.EqualTo((0, 0, 0)));
    }
}
