using System.Text.RegularExpressions;
using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The project view's context menus give each item an access key by the letter rule, unique in its menu, outside the
/// action's label, with any underscore of a name kept, and a disabled item's reason on its own line (baseline W26, W30;
/// findings B12, C9, C10, C-m3; checklist G58, G62, G82, G83).
/// </summary>
[TestFixture]
public class ProjectViewMenuKeysTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheLetterRule_TakesTheFirstLetter_ThenAnotherWordsFirst_ThenAnotherLetter_NeverIOrO()
    {
        IReadOnlyList<MenuEntryViewModel> entries = MenuAccessKeys.For(
        [
            Action("Properties"), Action("View spec"), Action("Accept spec…"), Action("Open records"), Action("Archive"),
        ]);

        Assert.That(entries.Select(entry => entry.Header), Is.EqualTo(new[]
        {
            "_Properties", "_View spec", "_Accept spec…", "Open _records", "Ar_chive",
        }));
    }

    [Test]
    public void ANameInALabel_KeepsItsUnderscores()
    {
        MenuEntryViewModel entry = MenuAccessKeys.For([Action("Open my_notes.md")]).Single();

        Assert.That(entry.Header, Is.EqualTo("Open _my__notes.md"), "B12: the name's own underscore is doubled, not taken as the key");
        Assert.That(entry.Action.Label, Is.EqualTo("Open my_notes.md"), "the button showing it has no key in its label");
    }

    [Test]
    public void ATextMark_IsNotPartOfTheMenuHeader()
    {
        Assert.That(MenuAccessKeys.For([Action("▶ Start")]).Single().Header, Is.EqualTo("_Start"), "C2: the menu shows words");
    }

    [Test]
    public void EveryMenuOfTheView_HasOneKeyPerItem_UniqueInItsMenu()
    {
        ProjectViewModel view = _views.BuildYourMarket();
        List<IReadOnlyList<MenuEntryViewModel>> menus =
        [
            .. view.AllNodes.SelectMany(node => node.Stages).Select(stage => stage.MenuEntries),
        ];

        Assert.That(menus, Is.Not.Empty);

        foreach (IReadOnlyList<MenuEntryViewModel> menu in menus)
        {
            List<char> keys = [.. menu.Select(entry => Key(entry.Header)).OfType<char>()];

            Assert.That(keys, Has.Count.EqualTo(menu.Count), string.Join(", ", menu.Select(entry => entry.Header)));
            Assert.That(keys, Is.Unique);
            Assert.That(keys, Has.None.EqualTo('I').And.None.EqualTo('O'));
        }
    }

    [Test]
    public void ADisabledItemsReason_IsItsSecondLine_NotPartOfItsHeader()
    {
        PieceActionViewModel check = new("Independent check…", () => { }, isEnabled: false, toolTip: "No scenarios yet")
        {
            Note = "No scenarios yet",
        };
        MenuEntryViewModel entry = MenuAccessKeys.For([check]).Single();

        Assert.That((entry.Header, entry.Note), Is.EqualTo(("Independent _check…", "No scenarios yet")), "I is never a key (G82)");
        Assert.That(entry.Header, Does.Not.Contain("No scenarios"), "W30: a menu label never changes with the state");
    }

    private static PieceActionViewModel Action(string label) => new(label, () => { });

    // The letter after the single underscore that marks the key, upper case; null when none is marked.
    private static char? Key(string header)
    {
        Match match = Regex.Match(header, "(?<!_)_(?!_)(.)");

        return match.Success ? char.ToUpperInvariant(match.Groups[1].Value[0]) : null;
    }
}
