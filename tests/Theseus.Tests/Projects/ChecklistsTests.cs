using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The confirmations that list items, as a checklist rather than a dense message box (Nigel, 2026-11-23, after pressing
/// Clear up verifiers…: "a very dense message box … a checklist view that is easier for the reader to understand and
/// action"): one row per item, each ticked to begin with, and a foot saying what the press will do.
/// </summary>
[TestFixture]
public class ChecklistsTests
{
    private static IReadOnlyList<WrongVerifier> Wrong() =>
    [
        new("verify-w1", "Verify: W1", "bym-w1", true, "it ran another project's scenarios, in C:\\h"),
        new("verify-w2", "Verify: W2", "bym-w2", false, "a copy: Verify: W2 (3) checks the same build"),
        new("write-bym", "Write holdout scenarios for BYM", "bym", false, "it writes scenarios in C:\\own, not this project's holdout C:\\h", IsWriter: true),
    ];

    [Test]
    public void ClearUp_HasARowPerCard_SayingWhatItChecks_Why_AndWhetherItRuns()
    {
        var list = Checklists.ClearUp(Wrong(), key => key == "bym-w1" ? "BYM W1" : key);

        Assert.That(list.Title, Is.EqualTo("Clear up cards"));
        Assert.That(list.ConfirmLabel, Is.EqualTo("Clear up"));
        Assert.That(list.Rows.Select(row => row.Key), Is.EqualTo(new[] { "verify-w1", "verify-w2", "write-bym" }));
        Assert.That(list.Rows[0], Is.EqualTo(new ChecklistRow("verify-w1", "Verify: W1", "Checks BYM W1 · running · card verify-w1",
            "It ran another project's scenarios, in C:\\h.")));
        Assert.That(list.Rows[2].Detail, Is.EqualTo("Writes scenarios for bym · not running · card write-bym"), "copies share a title");
    }

    [Test]
    public void ClearUp_SaysAtItsFoot_WhatThePressWillDo_ForTheRowsTicked()
    {
        var list = Checklists.ClearUp(Wrong(), key => key);

        Assert.That(list.Foot(["verify-w1", "verify-w2", "write-bym"]), Is.EqualTo("Stop 1 and archive 3"));
        Assert.That(list.Foot(["verify-w2"]), Is.EqualTo("Archive 1"));
        Assert.That(list.Foot([]), Is.EqualTo("Nothing ticked"));
    }

    [Test]
    public void StartTheChecks_HasARowPerBuild_AndSaysWhichCannotBeChecked()
    {
        var list = Checklists.StartChecks([("bym-w1", "BYM W1"), ("bym-w2", "BYM W2")], [("BYM W5", "it is still busy")]);

        Assert.That(list.Title, Is.EqualTo("Start the checks"));
        Assert.That(list.ConfirmLabel, Is.EqualTo("Start the checks"));
        Assert.That(list.Rows.Select(row => row.Label), Is.EqualTo(new[] { "BYM W1", "BYM W2" }));
        Assert.That(list.Intro, Does.Contain("Not checked: BYM W5, it is still busy."));
        Assert.That(list.Foot(["bym-w1", "bym-w2"]), Is.EqualTo("Make and start 2 verifiers"));
        Assert.That(list.Foot(["bym-w1"]), Is.EqualTo("Make and start 1 verifier"));
    }
}
