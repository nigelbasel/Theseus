using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Scenario writers Clear up… takes away (git-22, 2026-11-23): three made by old code write to a folder fixed in their
/// plan when they were made, not their project's holdout folder of today, so what they write would judge nothing.
/// </summary>
[TestFixture]
public class WrongWritersTests
{
    private const string Recorded = @"C:\src\Theseus-holdout";
    private const string Own = @"C:\Users\me\AppData\Local\Theseus\holdouts\theseus";

    [Test]
    public void AWriterAimedAtAnotherFolderThanItsProjectsHoldout_Goes()
    {
        var wrong = WrongWriters.Among([Writer("write-theseus", "theseus", Own, running: true)], FolderOf);

        Assert.That(wrong.Single().Key, Is.EqualTo("write-theseus"));
        Assert.That(wrong.Single().IsRunning, Is.True);
        Assert.That(wrong.Single().Why, Is.EqualTo($"it writes scenarios in {Own}, not this project's holdout {Recorded}"));
    }

    [Test]
    public void AWriterInItsProjectsHoldout_Stays_AndAnArchivedOneIsNotCounted()
    {
        Assert.That(WrongWriters.Among([Writer("write-theseus", "theseus", Recorded + @"\")], FolderOf), Is.Empty);
        Assert.That(WrongWriters.Among([Writer("write-old", "theseus", Own, archived: true)], FolderOf), Is.Empty);
    }

    [Test]
    public void AWriterWhoseProjectsHoldoutIsNotFound_IsNotJudged()
    {
        Assert.That(() => WrongWriters.Among([Writer("write-theseus", "theseus", Own)], _ => string.Empty), Throws.Nothing,
            "live read, 2026-11-23: a recorded holdout with no clone has no folder, and the project view failed");
        Assert.That(WrongWriters.Among([Writer("write-theseus", "theseus", Own)], _ => string.Empty), Is.Empty);
    }

    private static string FolderOf(string? project) => project == "theseus" ? Recorded : @"C:\ad-hoc";

    private static WriterRecord Writer(string key, string? project, string folder, bool running = false, bool archived = false) =>
        new(key, $"Write holdout scenarios for {key}", project, folder, running, archived);
}
