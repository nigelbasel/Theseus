using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// A read lands in two steps (PR 2 review, item 2): the catalogue first, which decides which cards leave the grid, and
/// the slugs' artefacts after it; before the first catalogue lands nothing is shown as readable, so no card is held out.
/// </summary>
[TestFixture]
public class BackgroundProjectReadsTwoStepTests
{
    private const string Folder = @"C:\repos\home";

    private static readonly ProjectFileReading Catalogue = ProjectFileReader.Read("p", "# P\n\n**Kit:** method-v0.2\n**Primary branch:** owner/p\n")
        with { Folder = Folder };

    [Test]
    public void TheCatalogueLandsFirst_ThenTheArtefacts_AndNothingBeforeTheCatalogue()
    {
        using var artefactsMayFinish = new ManualResetEventSlim(false);
        using var posted = new SemaphoreSlim(0);
        var seen = new List<string>();
        var artefacts = new Dictionary<string, WorkstreamArtefacts> { ["a-slug"] = WorkstreamArtefacts.NotOnMachine("a-slug") };

        var reads = new BackgroundProjectReads(_ => [Catalogue],
            action =>
            {
                action();
                posted.Release();
            },
            TimeSpan.FromSeconds(30),
            attach: readings =>
            {
                artefactsMayFinish.Wait(TimeSpan.FromSeconds(10));

                return [.. readings.Select(reading => reading with { Artefacts = artefacts })];
            });

        reads.Changed += (_, _) => seen.Add(reads.Current.Single().Artefacts.Count == 0 ? "catalogue" : "artefacts");

        reads.Request([Folder]);
        Assert.That(reads.Current, Is.Empty, "nothing is shown as readable before the catalogue lands");

        Assert.That(posted.Wait(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That((reads.Current.Single().IsReadable, reads.IsReading), Is.EqualTo((true, true)),
            "the catalogue is shown while the artefacts are still being read");

        artefactsMayFinish.Set();
        Assert.That(posted.Wait(TimeSpan.FromSeconds(10)), Is.True);

        Assert.That(seen, Is.EqualTo(new[] { "catalogue", "artefacts" }));
        Assert.That(reads.IsReading, Is.False);
    }
}
