using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

[TestFixture]
public class NewProjectDefaultsTests
{
    [TestCase("nigel", "shop", "nigel/shop")]
    [TestCase("Dana Smith", "checkout-redesign", "dana-smith/checkout-redesign")]
    [TestCase("José", "shop", "jose/shop")]
    [TestCase("", "shop", "shop")]
    [TestCase(null, "shop", "shop")]
    [TestCase("nigel", "", "")]
    [TestCase("nigel", null, "")]
    public void SuggestedPrimary_IsTheUserThenTheProject(string? user, string? name, string expected)
    {
        Assert.That(NewProjectDefaults.SuggestedPrimary(user, name), Is.EqualTo(expected));
    }

    [Test]
    public void TheSuggestion_IsABranchAGrantCanCover()
    {
        Assert.That(ProjectBranchRules.HasPrimary(NewProjectDefaults.SuggestedPrimary("Dana Smith", "shop")), Is.True);
    }

    [TestCase(true, "dana/shop", true)]
    [TestCase(false, "dana/shop", false)]
    [TestCase(true, "", false)]
    [TestCase(true, "main", false)]
    [TestCase(true, "<user>/<project>", false)]
    public void TheGrant_IsMadeOnlyWhenTickedForARealBranch(bool ticked, string primary, bool expected)
    {
        var details = new NewProjectDetails("shop", "Shop", string.Empty, primary) { GrantBranchRules = ticked };

        Assert.That(NewProjectDefaults.GrantsBranchRules(details), Is.EqualTo(expected));
    }

    [Test]
    public void DetailsMadeWithoutTheBox_GrantNothing()
    {
        Assert.That(NewProjectDefaults.GrantsBranchRules(new NewProjectDetails("shop", "", "", "dana/shop")), Is.False);
    }
}
