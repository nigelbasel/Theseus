using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Accept spec (project-view AC 7, 15): a confirmation naming the slug and the commit, and only a yes writes Theseus's
/// own store; a bug's spec is its rule.
/// </summary>
[TestFixture]
public class SpecAtGateTests
{
    private string _folder = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-spec-at-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    private static readonly WorkstreamArtefacts Artefacts =
        new("a-slug", new WorkstreamSource("work/a-slug"), new HashSet<string> { "spec.md" })
        {
            SpecText = "# Spec\n", SpecHash = "abcdef1234567", 
        };

    [Test]
    public void TheQuestion_NamesTheSlugAndTheVersion()
    {
        var spec = SpecAtGate.For("a-project", "a-slug", PipelineKind.Feature, Artefacts)!;

        Assert.That(spec.ConfirmationQuestion, Does.StartWith(
            "Accept the spec of a-slug in a-project, version abcdef1 (read from work/a-slug)?"));
    }

    [Test]
    public void OnlyAYes_WritesTheStore()
    {
        var store = new SpecAcceptanceStore(Path.Combine(_folder, "spec-acceptances.json"));
        var spec = SpecAtGate.For("a-project", "a-slug", PipelineKind.Feature, Artefacts)!;
        var asked = new List<string>();

        Assert.That(store.AcceptIfConfirmed(spec, question => { asked.Add(question); return false; }, DateTimeOffset.UnixEpoch), Is.False);
        Assert.That(store.AcceptIfConfirmed(spec, null, DateTimeOffset.UnixEpoch), Is.False, "no one to ask accepts nothing");
        Assert.That(store.Latest("a-project", "a-slug"), Is.Null);

        Assert.That(store.AcceptIfConfirmed(spec, _ => true, DateTimeOffset.UnixEpoch), Is.True);
        Assert.That(store.Latest("a-project", "a-slug"),
            Is.EqualTo(new SpecAcceptance("a-project", "a-slug", "abcdef1234567", DateTimeOffset.UnixEpoch)));
        Assert.That(asked.Single(), Is.EqualTo(spec.ConfirmationQuestion));
    }

    [Test]
    public void ABugsSpec_IsItsRule()
    {
        var bug = Artefacts with
        {
            FixText = "# Fix\n\n## Spec\n1. The rule.\n\n## Cause\nWhy.\n", RuleHash = "rule-hash",
        };

        var spec = SpecAtGate.For("a-project", "a-slug", PipelineKind.Bug, bug)!;

        Assert.That((spec.Text, spec.ContentHash), Is.EqualTo(("## Spec\n1. The rule.\n\n", "rule-hash")));
    }

    [Test]
    public void NoSpecRead_IsNoSpecAtTheGate()
    {
        Assert.That(SpecAtGate.For("a-project", "a-slug", PipelineKind.Feature, WorkstreamArtefacts.NotStarted("a-slug")), Is.Null);
    }
}
