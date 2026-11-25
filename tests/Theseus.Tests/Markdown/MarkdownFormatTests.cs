using Theseus.Core.Markdown;

namespace Theseus.Tests.Markdown;

/// <summary>Each toolbar button on a selection and on an empty one (1.1e AC 22, 25): it only inserts Markdown.</summary>
[TestFixture]
public class MarkdownFormatTests
{
    // The text after the change, with the selection marked « and ».
    private static string After(MarkdownAction action, string marked)
    {
        var start = marked.IndexOf('«', StringComparison.Ordinal);
        var end = marked.IndexOf('»', StringComparison.Ordinal);
        var text = marked.Remove(end, 1).Remove(start, 1);
        var change = MarkdownFormat.Apply(action, text, start, end - start - 1);
        var result = change.ApplyTo(text);

        return result.Insert(change.SelectionStart + change.SelectionLength, "»").Insert(change.SelectionStart, "«");
    }

    [TestCase(MarkdownAction.Bold, "a «word» b", "a **«word»** b")]
    [TestCase(MarkdownAction.Bold, "a «» b", "a **«bold text»** b")]
    [TestCase(MarkdownAction.Bold, "a **«word»** b", "a «word» b")]
    [TestCase(MarkdownAction.Bold, "a «**word**» b", "a «word» b")]
    [TestCase(MarkdownAction.Italic, "a «word» b", "a *«word»* b")]
    [TestCase(MarkdownAction.Italic, "«»", "*«italic text»*")]
    [TestCase(MarkdownAction.Italic, "a *«word»* b", "a «word» b")]
    [TestCase(MarkdownAction.Italic, "a **«word»** b", "a ***«word»*** b")]
    [TestCase(MarkdownAction.Italic, "a «**word**» b", "a *«**word**»* b")]
    [TestCase(MarkdownAction.Code, "«a\nb\n»c", "```\n«a\nb»\n```\nc")]
    [TestCase(MarkdownAction.Code, "x\r\n«a\r\nb»", "x\r\n```\r\n«a\r\nb»\r\n```")]
    [TestCase(MarkdownAction.Code, "run «dotnet test» now", "run `«dotnet test»` now")]
    [TestCase(MarkdownAction.Code, "«»", "`«code»`")]
    [TestCase(MarkdownAction.Code, "x\n«a\nb»", "x\n```\n«a\nb»\n```")]
    [TestCase(MarkdownAction.Link, "see «docs» here", "see [docs](«https://») here")]
    [TestCase(MarkdownAction.Link, "see «» here", "see [«link text»](https://) here")]
    [TestCase(MarkdownAction.Link, "«https://example.com»", "[«link text»](https://example.com)")]
    public void AnInlineButton_WrapsTheSelection_OrInsertsAPlaceholder_OrTakesItsMarkOff(MarkdownAction action, string before, string after)
    {
        Assert.That(After(action, before), Is.EqualTo(after));
    }

    [TestCase(MarkdownAction.Heading, "Ti«»tle", "## Title«»")]
    [TestCase(MarkdownAction.Heading, "## Ti«»tle", "Title«»")]
    [TestCase(MarkdownAction.BulletedList, "«one\ntwo»", "«- one\n- two»")]
    [TestCase(MarkdownAction.BulletedList, "«- one\n- two»", "«one\ntwo»")]
    [TestCase(MarkdownAction.BulletedList, "o«»ne", "- one«»")]
    [TestCase(MarkdownAction.NumberedList, "«one\ntwo\nthree»", "«1. one\n2. two\n3. three»")]
    [TestCase(MarkdownAction.NumberedList, "«- one\n- two»", "«1. one\n2. two»")]
    [TestCase(MarkdownAction.CheckBox, "«one\n  two»", "«- [ ] one\n  - [ ] two»")]
    [TestCase(MarkdownAction.CheckBox, "«- [ ] one»", "«one»")]
    [TestCase(MarkdownAction.CheckBox, "«- one»", "«- [ ] one»")]
    [TestCase(MarkdownAction.BulletedList, "before\n«a\n\nb»\nafter", "before\n«- a\n\n- b»\nafter")]
    [TestCase(MarkdownAction.Heading, "o«»ne\r\ntwo", "## one«»\r\ntwo")]
    [TestCase(MarkdownAction.BulletedList, "«one\r\ntwo»\r\nthree", "«- one\r\n- two»\r\nthree")]
    [TestCase(MarkdownAction.CheckBox, "«one\r\n»two", "«- [ ] one»\r\ntwo")]
    public void ALineButton_MarksEveryLineTheSelectionTouches_OrTakesTheMarkOff(MarkdownAction action, string before, string after)
    {
        Assert.That(After(action, before), Is.EqualTo(after));
    }

    [TestCase("- [ ] It reads", "It reads")]
    [TestCase("  2. It reads", "It reads")]
    [TestCase("* It reads", "It reads")]
    [TestCase("It reads", "It reads")]
    public void ACriterion_LosesTheListMarkTheToolbarPutThere(string line, string criterion)
    {
        Assert.That(MarkdownFormat.WithoutListMark(line), Is.EqualTo(criterion));
    }

    [Test]
    public void Search_FindsTheNextMatch_RoundToTheStart_AndCounts()
    {
        Assert.That((TextFind.Next("Abc abc", "ABC", 1), TextFind.Next("Abc abc", "abc", 5), TextFind.Next("x", "y", 0), TextFind.Count("aXaxa", "x")),
            Is.EqualTo((4, 0, -1, 2)));
    }
}
