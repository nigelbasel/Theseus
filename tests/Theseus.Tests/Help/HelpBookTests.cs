using Theseus.Core.Help;

namespace Theseus.Tests.Help;

[TestFixture]
public class HelpBookTests
{
    [Test]
    public void Sections_ExplainEveryButtonOnACard()
    {
        var terms = HelpBook.Sections.SelectMany(section => section.Entries).Select(entry => entry.Term);

        Assert.That(terms, Is.SupersetOf(new[] { "Start", "Stop", "Focus", "Adopt", "Refresh" }));
    }

    [Test]
    public void Sections_CoverStartingAndStoppingAsProcessesOfTheirOwn()
    {
        var titles = HelpBook.Sections.Select(section => section.Title).ToList();

        Assert.That(titles, Does.Contain("Starting a session"));
        Assert.That(titles, Does.Contain("Stopping a session"),
            "the two that run through parts nobody can see are the two worth explaining");
    }

    [Test]
    public void Sections_SayWhatToDoWhenStoppingDoesNothing()
    {
        var stopping = HelpBook.Sections.Single(section => section.Title == "Stopping a session");

        Assert.That(stopping.Entries.Count(entry => entry.Term.Contains("Nothing happens")), Is.EqualTo(2),
            "an unregistered hook and a session older than the hook look identical and are not");
    }

    [Test]
    public void Sections_AreAllWorthShowing()
    {
        foreach (var section in HelpBook.Sections)
        {
            Assert.That(section.Title, Is.Not.Empty);
            Assert.That(section.Preamble, Is.Not.Empty);
            Assert.That(section.Entries, Is.Not.Empty);

            foreach (var entry in section.Entries)
            {
                Assert.That(entry.Term, Is.Not.Empty);
                Assert.That(entry.Explanation, Is.Not.Empty, $"{entry.Term} is a heading with nothing under it");
            }
        }
    }
}
