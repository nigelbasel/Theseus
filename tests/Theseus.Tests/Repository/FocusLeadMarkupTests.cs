using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Nigel could not find how to focus a project's lead session: the header's "⧉ Focus" beside "Lead: … · idle" was easy
/// to miss (git-22, 2026-11-24). It is also on the Workstream menu with Ctrl+L, a free Ctrl+letter key for a wide-scope
/// command (G77) shown beside the item (G78), and an icon button in the project toolbar.
/// </summary>
[TestFixture]
public class FocusLeadMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Load(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]));

    [Test]
    public void TheWorkstreamMenu_HasFocusLeadSession_WithCtrlL()
    {
        var window = Load("MainWindow.xaml");
        var menu = window.Descendants(Presentation + "MenuItem").Single(item => (string?)item.Attribute("Header") == "_Workstream");
        var item = menu.Descendants(Presentation + "MenuItem").Single(each => (string?)each.Attribute("Command") == "{Binding FocusLeadCommand}");

        Assert.That((string?)item.Attribute("Header"), Is.EqualTo("Focus lea_d session"));
        Assert.That((string?)item.Attribute("InputGestureText"), Is.EqualTo("Ctrl+L"));
        Assert.That(window.Descendants(Presentation + "KeyBinding").Where(key => (string?)key.Attribute("Gesture") == "Ctrl+L")
            .Select(key => (string?)key.Attribute("Command")), Is.EqualTo(new[] { "{Binding FocusLeadCommand}" }));
        Assert.That(menu.Descendants(Presentation + "MenuItem").Select(each => (string?)each.Attribute("Header"))
            .Where(header => header is not null && !header.StartsWith('{'))
            .Select(header => char.ToUpperInvariant(header![header.IndexOf('_') + 1])), Is.Unique, "access keys unique in the menu (G82)");
    }

    [Test]
    public void TheProjectToolbar_HasAFocusLeadIcon_ThatSaysItsKey()
    {
        var button = Load("Views", "ProjectView.xaml").Descendants(Presentation + "Button")
            .Single(each => (string?)each.Attribute(Xaml + "Name") == "FocusLeadIcon");

        Assert.That((string?)button.Attribute("Command"), Is.EqualTo("{Binding FocusLeadCommand}"));
        Assert.That((string?)button.Attribute("ToolTip"), Is.EqualTo("{Binding FocusLeadIconToolTip}"));
        Assert.That((string?)button.Attribute("AutomationProperties.Name"), Is.EqualTo("Focus lead session"));
        Assert.That((string?)button.Attribute("Style"), Is.EqualTo("{StaticResource ToolbarIcon}"));
    }

    private static string RepositoryRoot([CallerFilePath] string path = "")
    {
        var at = new DirectoryInfo(Path.GetDirectoryName(path)!);

        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Theseus.slnx")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
