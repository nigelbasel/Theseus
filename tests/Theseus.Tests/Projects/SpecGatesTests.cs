using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The owner's gate as a session sees it (1.1e AC 28, 29): gates() for each state, accept_spec keeping the owner's words
/// and refusing a stale version, and the record of when, by whom and how.
/// </summary>
[TestFixture]
public class SpecGatesTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static SpecAtGate Spec(string text) =>
        new("bym", "w7", SpecIdentity.Hash(text), text, new WorkstreamSource(@"<projects>\bym\work\w7"));

    [Test]
    public void TheGate_ForEachState()
    {
        var spec = Spec("v2");
        var accepted = spec.Accept(Nine) with { By = "nigel", How = SpecAcceptance.OnTheBoard };
        var older = Spec("v1").Accept(Nine);
        var reported = new ReportedAcceptance("bym", "w7", spec.ContentHash, "w7", "W7", "yes, accept it", Nine);

        Assert.That(SpecGates.State(null, null, null).Status, Is.EqualTo(OwnerGateStatus.NotWritten));
        Assert.That(SpecGates.State(spec, null, null).Status, Is.EqualTo(OwnerGateStatus.WaitingForOwner));
        Assert.That(SpecGates.State(spec, older, reported).Status, Is.EqualTo(OwnerGateStatus.ReportedWaitingForConfirmation));
        Assert.That(SpecGates.State(spec, older, null).Status, Is.EqualTo(OwnerGateStatus.ChangedSinceAccepted));
        Assert.That(SpecGates.State(spec, accepted, reported), Is.EqualTo(new OwnerGate("spec", spec.ShortVersion, OwnerGateStatus.Accepted) { Acceptance = accepted }));
        Assert.That(SpecGates.State(spec, accepted, null).IsPassed, Is.True);
    }

    [Test]
    public void GatesSays_WhereItStands_AndWhetherToPlan()
    {
        var spec = Spec("v2");
        var accepted = spec.Accept(Nine) with { By = "nigel", How = SpecAcceptance.SaidIn("W7") };

        Assert.That(SpecGates.Describe(SpecGates.State(spec, accepted, null), "bym", "w7", TimeZoneInfo.Utc),
            Is.EqualTo($"The spec of w7 in bym, version {spec.ShortVersion}: accepted (version {spec.ShortVersion}, 22 Nov 2026, 09:00, by nigel, said in W7's tab, confirmed on the board). You may plan."));
        Assert.That(SpecGates.Describe(SpecGates.State(spec, null, null), "bym", "w7", TimeZoneInfo.Utc), Does.Contain("waiting for the owner. Do not plan yet"));
        Assert.That(SpecGates.Describe(SpecGates.State(null, null, null), "bym", "w7", TimeZoneInfo.Utc), Does.Contain("has no spec written yet"));
    }

    [Test]
    public void AReport_IsKeptForTheCurrentVersion_AndRefusedForAStaleOne()
    {
        var spec = Spec("v2");

        var (report, refusal) = SpecGates.Report(spec, spec.ShortVersion, "w7", "W7", " yes, accept it ", Nine);

        Assert.That((refusal, report), Is.EqualTo(((string?)null, new ReportedAcceptance("bym", "w7", spec.ContentHash, "w7", "W7", "yes, accept it", Nine))));
        Assert.That(SpecGates.Report(spec, Spec("v1").ShortVersion, "w7", "W7", "yes", Nine).Refusal,
            Does.StartWith($"Version {Spec("v1").ShortVersion} is not the spec's current text, which is version {spec.ShortVersion}"));
        Assert.That(SpecGates.Report(spec, "abc", "w7", "W7", "yes", Nine).Refusal, Does.Contain("is not the spec's current text"), "too short to tell");
        Assert.That(SpecGates.Report(null, "abc1234", "w7", "W7", "yes", Nine).Refusal, Does.StartWith("This workstream has no spec written"));
        Assert.That(SpecGates.Report(spec, spec.ShortVersion, "w7", "W7", " ", Nine).Refusal, Does.StartWith("Quote the owner's words"));
    }

    [Test]
    public void TheSpecOfAWorkstream_IsReadFromItsProject()
    {
        var reading = ProjectFileReader.Read("bym", TestProjectsFolder.ProjectText("BYM", body: TestProjectsFolder.OneObjective("w7"))) with
        {
            Artefacts = new Dictionary<string, WorkstreamArtefacts>
            {
                ["w7"] = new WorkstreamArtefacts("w7", new WorkstreamSource(@"<projects>\bym\work\w7"), new HashSet<string> { "spec.md" })
                {
                    SpecText = "v2",
                    SpecHash = SpecIdentity.Hash("v2"),
                },
            },
        };

        Assert.That(SpecGates.SpecOf(reading, "w7")?.ContentHash, Is.EqualTo(SpecIdentity.Hash("v2")));
        Assert.That(SpecGates.SpecOf(reading, "other"), Is.Null);
    }

    [Test]
    public void OnlyTheCardsOwnWorkstream_WithNoRoleOfItsOwn_MayReport()
    {
        Assert.That(SpecGates.WhoMayReport("bym", "w7", Theseus.Core.Domain.WorkstreamRole.None, "w7", false, "abc1234"), Is.Null);
        Assert.That(SpecGates.WhoMayReport("bym", "w7", Theseus.Core.Domain.WorkstreamRole.None, "w8", false, "abc1234"),
            Is.EqualTo("You can report only for your own workstream, w7."));
        Assert.That(SpecGates.WhoMayReport("bym", "w7", Theseus.Core.Domain.WorkstreamRole.None, "w7", true, "abc1234"),
            Is.EqualTo("Version abc1234 is accepted already: call gates()."));
        Assert.That(SpecGates.WhoMayReport(null, null, Theseus.Core.Domain.WorkstreamRole.None, "w7", false, null), Does.Contain("not in a project"));
        Assert.That(SpecGates.WhoMayReport("bym", "verify-w7", Theseus.Core.Domain.WorkstreamRole.Verifier, "verify-w7", false, null),
            Is.EqualTo(SpecGates.NoGateForRole), "a verifier has no gate, and is never told to write a spec");
    }

    [Test]
    public void TheQuestion_NamesTheCardItReaches_AndTheOwnersWordsWhenReported()
    {
        var spec = Spec("v2");
        var report = new ReportedAcceptance("bym", "w7", spec.ContentHash, "w7", "W7", "yes, accept it", Nine);

        Assert.That(spec.QuestionFor("W7"), Does.EndWith("Theseus then tells W7 to carry on to the plan."));
        Assert.That(spec.QuestionFor("W7", cardIsRunning: false), Does.EndWith("W7 is stopped: Theseus then starts it, told to carry on to the plan."));
        Assert.That(spec.QuestionFor(null), Does.EndWith("No card works it, so no session will be told."));
        Assert.That(spec.QuestionFor("W7", report), Does.Contain("W7's session reports you said: \"yes, accept it\""));
        Assert.That(spec.ConfirmationQuestion, Does.Not.Contain("tells"), "with no card, none is named");
    }

    [Test]
    public void TheRecord_KeepsWhoAndHow_AndAnOldRecordReadsAsOnTheBoard()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"theseus-acceptances-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            var store = new SpecAcceptanceStore(Path.Combine(folder, "acceptances.json"));
            var spec = Spec("v2");

            Assert.That(store.AcceptIfConfirmed(spec, _ => true, Nine, spec.QuestionFor("W7"), "nigel", SpecAcceptance.SaidIn("W7")), Is.True);

            var kept = store.Latest("bym", "w7")!;
            Assert.That((kept.By, kept.How), Is.EqualTo(("nigel", "said in W7's tab, confirmed on the board")));
            Assert.That(new SpecAcceptance("bym", "w7", spec.ContentHash, Nine).Describe(TimeZoneInfo.Utc), Does.EndWith(", on the board"));
            Assert.That(store.AcceptIfConfirmed(Spec("v3"), _ => false, Nine, "?", "nigel", null), Is.False, "a No accepts nothing");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void AReport_IsReplacedByALaterOne_AndRemovedOnceConfirmed()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"theseus-reported-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            var store = new ReportedAcceptanceStore(Path.Combine(folder, "reported.json"));
            store.Report(new ReportedAcceptance("bym", "w7", "a", "w7", "W7", "first", Nine));
            store.Report(new ReportedAcceptance("bym", "w7", "b", "w7", "W7", "second", Nine));
            store.Report(new ReportedAcceptance("bym", "w8", "c", "w8", "W8", "other", Nine));

            Assert.That(store.For("bym", "w7")?.OwnerWords, Is.EqualTo("second"));

            store.Remove("bym", "w7");

            Assert.That((store.For("bym", "w7"), store.Load().Count), Is.EqualTo(((ReportedAcceptance?)null, 1)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
