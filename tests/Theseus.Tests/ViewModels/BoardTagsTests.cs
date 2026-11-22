using Theseus.App.ViewModels;
using Theseus.Core.Finding;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Tags on the board (card-search-and-tags items 9 to 17): adding and removing them on one card or several, on project
/// cards, shown on the card, kept across restarts, renamed and deleted everywhere, the Tags list in Any and All, #tag in
/// the box, words and tags together, and Clear putting the board back.
/// </summary>
[TestFixture]
public class BoardTagsTests
{
    private static BoardFixture Board()
    {
        var fixture = new BoardFixture()
            .With("bym-w7").Retitle("bym-w7", "BYM MCP W7 search")
            .With("bym-w6").Retitle("bym-w6", "BYM MCP W6 tables")
            .With("gdpr").Retitle("gdpr", "GDPR whitepaper")
            .Tag("bym-w7", "bym", "security")
            .Tag("bym-w6", "bym")
            .Tag("gdpr", "security", "waiting-on-dpo");
        fixture.Board.Refresh();

        return fixture;
    }

    private static WorkstreamCardViewModel CardOf(BoardFixture fixture, string key) => fixture.Card(key)!;

    private static string[] Shown(BoardFixture fixture) =>
        [.. fixture.Board.Cards.Where(card => !card.IsSearchDimmed).Select(card => card.Key).Order()];

    [Test]
    public void Tags_AreReadFromTheBoardFile_AndSurviveARestart()
    {
        var fixture = Board();
        fixture.Board.Tags.Set([CardOf(fixture, "gdpr")], "legal", on: true);

        var reopened = fixture.Reopen();
        reopened.Refresh();

        Assert.That(reopened.Cards.Single(card => card.Key == "gdpr").Tags, Is.EqualTo(new[] { "security", "waiting-on-dpo", "legal" }));
    }

    [Test]
    public void SettingATag_OnSeveralSelectedCards_FromOneCardsMenu_SetsItOnAll()
    {
        var fixture = Board();
        fixture.Board.ClickSelect(CardOf(fixture, "bym-w6"), toggle: false, extend: false);
        fixture.Board.ClickSelect(CardOf(fixture, "gdpr"), toggle: true, extend: false);

        var menu = fixture.Board.Tags.MenuFor(CardOf(fixture, "gdpr"));
        menu.Single(item => item.Header == "bym").Command.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(menu.Single(item => item.Header == "bym").IsChecked, Is.False, "not every selected card had it");
            Assert.That(CardOf(fixture, "gdpr").Tags, Does.Contain("bym"));
            Assert.That(CardOf(fixture, "bym-w6").Tags, Does.Contain("bym"));
        });
    }

    [Test]
    public void TheMenu_TicksATagEveryTargetHas_AndChoosingItTakesItOff()
    {
        var fixture = Board();
        var card = CardOf(fixture, "bym-w7");

        var menu = fixture.Board.Tags.MenuFor(card);
        var bym = menu.Single(item => item.Header == "bym");
        bym.Command.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(bym.IsChecked, Is.True);
            Assert.That(card.Tags, Is.EqualTo(new[] { "security" }));
            Assert.That(menu.Last().Header, Is.EqualTo("New tag…"));
            Assert.That(menu.Select(item => item.Header), Does.Contain("waiting-on-dpo"), "every tag in use is listed");
        });
    }

    [Test]
    public void NewTag_AsksForAName_AndRefusesAnInvalidOne_SayingWhy()
    {
        var fixture = Board();
        var said = new List<string>();
        fixture.Board.Tags.Say = said.Add;
        var answers = new Queue<string?>(["Needs Review", "bad/tag"]);
        fixture.Board.Tags.AskNewTag = () => answers.Dequeue();
        var card = CardOf(fixture, "gdpr");

        fixture.Board.Tags.MenuFor(card).Last().Command.Execute(null);
        fixture.Board.Tags.MenuFor(card).Last().Command.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(card.Tags, Does.Contain("needs-review"));
            Assert.That(said, Is.EqualTo(new[] { "No tag added: A tag is letters, digits and hyphens." }));
        });
    }

    [Test]
    public void ACard_ShowsAtMostThreeTags_InTheirColours_ThenPlusTheRest()
    {
        var fixture = Board().Tag("gdpr", "a", "b", "c", "d", "e");
        fixture.Board.Refresh();
        var card = CardOf(fixture, "gdpr");

        Assert.Multiple(() =>
        {
            Assert.That(card.TagChips.Select(chip => chip.Label), Is.EqualTo(new[] { "#a", "#b", "#c" }));
            Assert.That(card.MoreTags, Is.EqualTo("+2"));
            Assert.That(card.MoreTagsToolTip, Does.Contain("#e"));
        });
    }

    [Test]
    public void ATagFirstUsed_TakesAColourOfItsOwn_WhichOptionsCanChange()
    {
        var fixture = Board();
        fixture.Board.Tags.Set([CardOf(fixture, "gdpr")], "legal", on: true);
        var first = fixture.Board.Tags.ColourOf("legal");

        fixture.Board.Tags.SetColour("legal", TagPalette.Colours[^1]);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Settings.TagColours.ContainsKey("legal"), Is.True, "chosen on first use");
            Assert.That(TagPalette.Colours, Does.Contain(first));
            Assert.That(CardOf(fixture, "gdpr").TagChips.Single(chip => chip.Tag == "legal").ColourHex, Is.EqualTo(TagPalette.Colours[^1]));
        });
    }

    [Test]
    public void ClickingATagOnACard_FiltersByIt()
    {
        var fixture = Board();

        CardOf(fixture, "gdpr").TagChips.Single(chip => chip.Tag == "waiting-on-dpo").FilterCommand.Execute("waiting-on-dpo");

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Search.TickedTags, Is.EqualTo(new[] { "waiting-on-dpo" }));
            Assert.That(Shown(fixture), Is.EqualTo(new[] { "gdpr" }));
        });
    }

    [Test]
    public void TheTagsList_CountsEachTag_AndTickingFilters_InAnyOrAll()
    {
        var fixture = Board();
        var search = fixture.Board.Search;
        search.LoadTagRows(fixture.Board.Tags.InUse(), fixture.Board.Tags.ColourOf);

        search.TagRows.Single(row => row.Tag == "bym").IsTicked = true;
        search.TagRows.Single(row => row.Tag == "security").IsTicked = true;
        var any = Shown(fixture);
        search.IsAll = true;
        var all = Shown(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(search.TagRows.Select(row => (row.Tag, row.Count)),
                Is.EqualTo(new[] { ("bym", 2), ("security", 2), ("waiting-on-dpo", 1) }));
            Assert.That(any, Is.EqualTo(new[] { "bym-w6", "bym-w7", "gdpr" }));
            Assert.That(all, Is.EqualTo(new[] { "bym-w7" }));
            Assert.That(search.TagsButtonText, Is.EqualTo("Tags (2) ▾"));
        });
    }

    [Test]
    public void AHashTagInTheBox_IsTheSameAsTickingIt()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "#waiting-on-dpo";

        Assert.That(Shown(fixture), Is.EqualTo(new[] { "gdpr" }));
    }

    [Test]
    public void WordsAndTags_Combine()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "#bym w7";

        Assert.Multiple(() =>
        {
            Assert.That(Shown(fixture), Is.EqualTo(new[] { "bym-w7" }));
            Assert.That(fixture.Board.Search.FilteredLine, Is.EqualTo("Filtered: #bym · 'w7' · 1 of 3"));
        });
    }

    [Test]
    public void ACardHoldingTheWords_ButNotTheTag_IsDimmed_AndNotHighlighted()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "#bym gdpr";

        Assert.Multiple(() =>
        {
            Assert.That(CardOf(fixture, "gdpr").IsSearchDimmed, Is.True);
            Assert.That(CardOf(fixture, "gdpr").TitleHighlights, Is.Empty, "only a match is highlighted (item 3)");
        });
    }

    [Test]
    public void AHashTagTyped_IsTickedInTheList_AndCountedOnTheButton_AndUntickingItTakesItOutOfTheBox()
    {
        var fixture = Board();
        var search = fixture.Board.Search;
        search.LoadTagRows(fixture.Board.Tags.InUse(), fixture.Board.Tags.ColourOf);

        search.Text = "#bym w7";
        var ticked = search.TagRows.Single(row => row.Tag == "bym").IsTicked;
        var button = search.TagsButtonText;
        search.TagRows.Single(row => row.Tag == "bym").IsTicked = false;

        Assert.Multiple(() =>
        {
            Assert.That(ticked, Is.True, "review T4");
            Assert.That(button, Is.EqualTo("Tags (1) ▾"));
            Assert.That(search.Text, Is.EqualTo("w7"));
        });
    }

    [Test]
    public void ATickedTagNoCardHasAnyMore_KeepsItsRow_SoItCanBeUnticked()
    {
        var fixture = Board();
        var search = fixture.Board.Search;
        search.TickedTags.Add("waiting-on-dpo");
        fixture.Board.Tags.Set([CardOf(fixture, "gdpr")], "waiting-on-dpo", on: false);

        search.LoadTagRows(fixture.Board.Tags.InUse(), fixture.Board.Tags.ColourOf);
        var row = search.TagRows.Single(candidate => candidate.Tag == "waiting-on-dpo");
        row.IsTicked = false;

        Assert.Multiple(() =>
        {
            Assert.That(row.Count, Is.Zero, "review T5");
            Assert.That(search.TickedTags, Is.Empty);
        });
    }

    [Test]
    public void RenamingOrDeletingATag_FollowsItIntoTheBox()
    {
        var fixture = Board();
        fixture.Board.Search.Text = "#bym w7";

        fixture.Board.Tags.Rename("bym", "build");
        var renamed = fixture.Board.Search.Text;
        fixture.Board.Tags.Delete("build");

        Assert.Multiple(() =>
        {
            Assert.That(renamed, Is.EqualTo("#build w7"), "review T6");
            Assert.That(fixture.Board.Search.Text, Is.EqualTo("w7"));
        });
    }

    [Test]
    public void HideTheRest_WithATag_HidesTheCardsWithoutIt()
    {
        var fixture = Board();
        fixture.Board.Search.TickedTags.Add("waiting-on-dpo");

        fixture.Board.Search.HideRest = true;

        Assert.That(fixture.Board.GridCards.Select(card => card.Key), Is.EqualTo(new[] { "gdpr" }));
    }

    [Test]
    public void OptionsTags_MarksTheTagsColour_AndMovesTheMarkWhenItChanges()
    {
        var fixture = Board();
        var row = new TagsViewModel(fixture.Board.Tags).Rows.Single(candidate => candidate.Tag == "bym");

        row.Swatches[^1].Command.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(row.Swatches.Single(swatch => swatch.IsCurrent).ColourHex, Is.EqualTo(TagPalette.Colours[^1]), "review T8");
            Assert.That(row.ColourHex, Is.EqualTo(TagPalette.Colours[^1]));
        });
    }

    [Test]
    public void Clear_UnticksTheTags_AndEmptiesTheBox()
    {
        var fixture = Board();
        fixture.Board.Search.TickedTags.Add("bym");
        fixture.Board.Search.Text = "search";

        fixture.Board.Search.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Search.TickedTags, Is.Empty);
            Assert.That(Shown(fixture), Has.Length.EqualTo(3));
        });
    }

    [Test]
    public void Rename_ChangesTheTagOnEveryCard_KeepsItsColour_AndItsTick()
    {
        var fixture = Board();
        fixture.Board.Tags.SetColour("bym", TagPalette.Colours[^1]);
        var colour = fixture.Board.Tags.ColourOf("bym");
        fixture.Board.Search.TickedTags.Add("bym");

        var count = fixture.Board.Tags.Rename("bym", "build");

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(fixture.Sidecars.SelectMany(sidecar => sidecar.Tags), Does.Not.Contain("bym"));
            Assert.That(CardOf(fixture, "bym-w6").Tags, Is.EqualTo(new[] { "build" }));
            Assert.That(fixture.Board.Tags.ColourOf("build"), Is.EqualTo(colour));
            Assert.That(fixture.Board.Search.TickedTags, Is.EqualTo(new[] { "build" }));
        });
    }

    [Test]
    public void Delete_TakesTheTagOffEveryCard_AndUnticksIt()
    {
        var fixture = Board();
        fixture.Board.Search.TickedTags.Add("security");

        var count = fixture.Board.Tags.Delete("security");

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(fixture.Board.Tags.InUse().Select(use => use.Tag), Is.EqualTo(new[] { "bym", "waiting-on-dpo" }));
            Assert.That(fixture.Board.Search.TickedTags, Is.Empty);
        });
    }

    [Test]
    public void AProjectCard_CarriesTags_InSettings_AndIsFoundByThem()
    {
        var fixture = Board();
        var project = new ProjectCardViewModel("bym-mcp", "BuildYourMarket MCP");
        fixture.Board.ShowProjects([project]);

        fixture.Board.Tags.Set([project], "client", on: true);
        fixture.Board.Search.Text = "#client";

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Settings.ProjectTags["bym-mcp"], Is.EqualTo(new[] { "client" }));
            Assert.That(fixture.Board.Matches, Is.EqualTo(new IBoardCard[] { project }));
            Assert.That(project.TagChips.Select(chip => chip.Tag), Is.EqualTo(new[] { "client" }));
        });
    }

    [Test]
    public void AProjectCardsTags_AreRenamedAndDeleted_WithEveryCardsTags()
    {
        var fixture = Board();
        var project = new ProjectCardViewModel("bym-mcp", "BuildYourMarket MCP");
        fixture.Board.ShowProjects([project]);
        fixture.Board.Tags.SetProject("bym-mcp", ["bym"]);

        fixture.Board.Tags.Rename("bym", "build");
        var renamed = project.Tags;
        fixture.Board.Tags.Delete("build");

        Assert.Multiple(() =>
        {
            Assert.That(renamed, Is.EqualTo(new[] { "build" }));
            Assert.That(project.Tags, Is.Empty);
        });
    }

    [Test]
    public void OptionsTags_ListsEachTag_AndDeleteAsksFirst_NamingTheCount()
    {
        var fixture = Board();
        var options = new TagsViewModel(fixture.Board.Tags);
        var security = options.Rows.Single(row => row.Tag == "security");

        security.DeleteCommand.Execute(null);
        var stillThere = fixture.Board.Tags.InUse().Any(use => use.Tag == "security");
        var question = security.DeleteQuestion;
        security.ConfirmDeleteCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(stillThere, Is.True, "nothing is deleted before the confirmation");
            Assert.That(question, Is.EqualTo("Take #security off 2 cards? Its colour goes too."));
            Assert.That(options.Rows.Select(row => row.Tag), Is.EqualTo(new[] { "bym", "waiting-on-dpo" }));
        });
    }

    [Test]
    public void OptionsTags_Rename_IsRefusedWithAReason_UntilANewValidNameIsTyped()
    {
        var fixture = Board();
        var row = new TagsViewModel(fixture.Board.Tags).Rows.Single(candidate => candidate.Tag == "bym");

        var unchanged = row.WhyNotRename;
        row.NewName = "a/b";
        var invalid = row.WhyNotRename;
        row.NewName = "Build";

        Assert.Multiple(() =>
        {
            Assert.That(unchanged, Is.EqualTo("Type a new name first"));
            Assert.That(invalid, Is.EqualTo("A tag is letters, digits and hyphens"));
            Assert.That(row.CanRename, Is.True);
            Assert.That(row.RenameToolTip, Is.EqualTo("Rename #bym to #build on 2 cards"));
        });
    }

    [Test]
    public void TheTagsField_Adds_OffersTagsInUse_AndRefusesARepeat()
    {
        var changes = 0;
        var editor = new TagEditorViewModel(() => ["bym", "security", "legal"], () => changes++);
        editor.Load(["bym"]);

        editor.NewTag = "se";
        var offered = editor.Suggestions;
        editor.NewTag = "BYM";
        var repeat = editor.WhyNotAdd;
        editor.NewTag = "Waiting on DPO";
        editor.AddCommand.Execute(null);
        editor.RemoveCommand.Execute("bym");

        Assert.Multiple(() =>
        {
            Assert.That(offered, Is.EqualTo(new[] { "security" }));
            Assert.That(repeat, Is.EqualTo("The card already has this tag"));
            Assert.That(editor.Tags, Is.EqualTo(new[] { "waiting-on-dpo" }));
            Assert.That(changes, Is.EqualTo(2));
        });
    }

    [Test]
    public void Enter_WithARefusedTag_AddsNothing_AndSaysWhyUnderTheBox()
    {
        var editor = new TagEditorViewModel(() => [], () => { });
        editor.Load([]);

        editor.NewTag = "a/b";
        editor.EnterCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.EnterCommand.CanExecute(null), Is.True, "Enter is handled, so it never presses a dialog's OK (review T7)");
            Assert.That(editor.Tags, Is.Empty);
            Assert.That(editor.Refusal, Is.EqualTo("A tag is letters, digits and hyphens"));
        });
    }

    [Test]
    public void ATagTypedButNotAdded_IsAddedAtOK()
    {
        var editor = new TagEditorViewModel(() => [], () => { });
        editor.Load(["bym"]);
        editor.NewTag = "security";

        editor.AddPending();

        Assert.That(editor.MergeInto(["bym"]), Is.EqualTo(new[] { "bym", "security" }));
    }
}
