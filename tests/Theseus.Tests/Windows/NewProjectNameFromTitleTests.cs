using System.Windows;
using System.Windows.Controls;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// New project asks for the title first and makes the name from it, as a slug the user can change (Nigel, 2026-11-24):
/// a name changed by hand stays as it is, and a cleared name follows the title again.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class NewProjectNameFromTitleTests
{
    [Test]
    public void TheTitle_ComesBeforeTheName()
    {
        using OpenedWindow opened = OpenedWindow.Of("NewProject");
        Panel fields = (Panel)opened.Named<TextBox>("TitleBox").Parent;

        Assert.That(fields.Children.IndexOf(opened.Named<Label>("TitleLabel")),
            Is.LessThan(fields.Children.IndexOf(opened.Named<Label>("NameLabel"))));
    }

    [Test]
    public void TypingATitle_MakesTheName()
    {
        using OpenedWindow opened = OpenedWindow.Of("NewProject");

        opened.Named<TextBox>("TitleBox").Text = "LeadPro MCP server";

        Assert.That(opened.Named<TextBox>("NameBox").Text, Is.EqualTo("leadpro-mcp-server"));
        Assert.That(opened.Named<Button>("CreateButton").IsEnabled, Is.True);
    }

    [Test]
    public void ANameChangedByHand_IsKept_UntilItIsCleared()
    {
        using OpenedWindow opened = OpenedWindow.Of("NewProject");
        TextBox title = opened.Named<TextBox>("TitleBox");
        TextBox name = opened.Named<TextBox>("NameBox");

        name.Text = "lp-mcp";
        title.Text = "LeadPro MCP server";

        Assert.That(name.Text, Is.EqualTo("lp-mcp"));

        name.Text = "";
        title.Text = "LeadPro MCP";

        Assert.That(name.Text, Is.EqualTo("leadpro-mcp"));
    }

    [Test]
    public void ConvertToProject_KeepsTheCardsName()
    {
        Window window = DialogHost.Open(new NewProjectWindow("auth-spike", "Auth research"));

        try
        {
            Assert.That(((TextBox)window.FindName("NameBox")).Text, Is.EqualTo("auth-spike"));
        }
        finally
        {
            window.Close();
        }
    }
}
