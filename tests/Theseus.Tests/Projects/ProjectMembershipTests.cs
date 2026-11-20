using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.Projects;

/// <summary>
/// A card belongs to at most one project and holds one current slug; two cards share a slug only when one is a
/// verifier (spec AC 4 and 10).
/// </summary>
[TestFixture]
public class ProjectMembershipTests
{
    [Test]
    public void ASlugNoOtherCardHolds_CanBeTaken()
    {
        var card = Card("one");

        Assert.That(ProjectMembership.Refusal([card, Card("two", "p", "other")], card, "p", "a-slug"), Is.Null);
    }

    [Test]
    public void ASlugAnotherCardHolds_IsRefused()
    {
        var card = Card("one");

        Assert.That(ProjectMembership.Refusal([card, Card("two", "p", "a-slug")], card, "p", "a-slug"),
            Is.EqualTo("two already works under a-slug in p. Two cards share a slug only when one is a verifier."));
    }

    [Test]
    public void AVerifier_SharesItsSubjectsSlug_AndASubjectMayJoinBesideAVerifier()
    {
        var verifier = Card("checker", role: WorkstreamRole.Verifier);
        var subject = Card("work");

        Assert.That(ProjectMembership.Refusal([verifier, Card("work", "p", "a-slug")], verifier, "p", "a-slug"), Is.Null);
        Assert.That(ProjectMembership.Refusal([subject, Card("checker", "p", "a-slug", WorkstreamRole.Verifier)], subject, "p", "a-slug"),
            Is.Null);
    }

    [Test]
    public void TheSameSlugInAnotherProject_OrOnAnArchivedCard_IsNoClash()
    {
        var card = Card("one");

        Assert.That(ProjectMembership.Refusal([card, Card("two", "other-project", "a-slug")], card, "p", "a-slug"), Is.Null);
        Assert.That(ProjectMembership.Refusal([card, Card("two", "p", "a-slug", archived: true)], card, "p", "a-slug"), Is.Null);
    }

    [Test]
    public void NoSlugYet_OrNoProject_IsNeverRefused()
    {
        var card = Card("one");

        Assert.That(ProjectMembership.Refusal([card, Card("two", "p", null)], card, "p", null), Is.Null);
        Assert.That(ProjectMembership.Refusal([card], card, null, null), Is.Null);
    }

    [Test]
    public void LeavingAProject_ClearsTheSlugToo()
    {
        Assert.That(ProjectMembership.Join(Card("one", "p", "a-slug"), null, "a-slug"),
            Has.Property(nameof(Workstream.Project)).Null.And.Property(nameof(Workstream.Slug)).Null);
    }

    [Test]
    public void ACardFromAProposal_InheritsItsProposersProject_WithTheProposedSlug()
    {
        var joined = ProjectMembership.FromProposal(Card("new"), Card("proposer", "p", "their-slug"), "new-slug");

        Assert.That((joined.Project, joined.Slug), Is.EqualTo(("p", "new-slug")));
    }

    [Test]
    public void ACardFromAnAdHocProposer_StaysAdHoc_WhateverSlugWasProposed()
    {
        var joined = ProjectMembership.FromProposal(Card("new"), Card("proposer"), "new-slug");

        Assert.That((joined.Project, joined.Slug), Is.EqualTo(((string?)null, (string?)null)));
    }

    [Test]
    public void AVerifierCard_TakesItsSubjectsProjectAndSlug()
    {
        var verifier = ProjectMembership.ForVerifier(Card("checker", role: WorkstreamRole.Verifier), Card("work", "p", "a-slug"));

        Assert.That((verifier.Project, verifier.Slug), Is.EqualTo(("p", "a-slug")));
    }

    [TestCase("a-slug", true)]
    [TestCase("slug2", true)]
    [TestCase("A-Slug", false)]
    [TestCase("a slug", false)]
    [TestCase("-a", false)]
    [TestCase("", false)]
    public void ASlug_IsLowerCaseLettersDigitsAndHyphens(string text, bool isSlug)
    {
        Assert.That(ProjectMembership.IsSlug(text), Is.EqualTo(isSlug));
    }
}
