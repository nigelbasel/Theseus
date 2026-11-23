using Grouping = Theseus.Core.Settings.ProjectViewGrouping;
using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Project views over the accepted mockup's two projects (1.1d PR 1's fixtures), with what their buttons ran recorded,
/// for the view model, layout and markup tests of PR 2.
/// </summary>
internal sealed class ProjectViews
{
    private readonly TestProjectsFolder _folder;

    public ProjectViews(TestProjectsFolder folder) => _folder = folder;

    /// <summary>Where folds and the pane's width are kept; a view built again over it is Theseus restarted.</summary>
    public MemorySettingsStore Settings { get; } = Flow();

    // The 1.1d and 1.1f view tests read the rounds-and-work lens; the objectives lens is the default (Nigel, 2026-11-22),
    // which ObjectivesLensTests checks on a store of its own.
    private static MemorySettingsStore Flow()
    {
        var store = new MemorySettingsStore();
        store.Write(store.Read() with
        {
            ProjectGrouping = new Dictionary<string, Grouping>(StringComparer.OrdinalIgnoreCase)
            {
                [TheseusProject] = Grouping.Flow,
                [ProjectViewFixtures.BuildYourMarket] = Grouping.Flow,
                [OneProject] = Grouping.Flow,
                ["bym"] = Grouping.Flow,
                ["theseus-clarity"] = Grouping.Flow,
            },
        });

        return store;
    }

    /// <summary>What the buttons ran, in order, such as "AcceptSpec:project-view" or "Focus:git-22".</summary>
    public List<string> Ran { get; } = [];

    /// <summary>The criteria ticked and objectives edited and added, in order, such as "Tick:12:True", "Edit:1.1" or "Add:1".</summary>
    public List<string> Edited { get; } = [];

    /// <summary>What the view said in the status bar, in order.</summary>
    public List<string> Notices { get; } = [];

    public ProjectViewActions Actions => new(
        spec => Ran.Add($"OpenSpec:{spec.Slug}"),
        spec => Ran.Add($"AcceptSpec:{spec.Slug}"),
        key => Ran.Add($"Verify:{key}"),
        key => Ran.Add($"Focus:{key}"),
        key => Ran.Add($"Start:{key}"),
        key => Ran.Add($"Stop:{key}"),
        path => Ran.Add($"Open:{path}"),
        Notices.Add,
        (project, key) => Ran.Add($"AddToProject:{project}:{key}"));

    public ProjectViewEdits Edits => new((_, criterion, ticked) => Edited.Add($"Tick:{criterion.Line}:{ticked}"),
        objective => Edited.Add($"Edit:{objective.Number}"),
        phase => Edited.Add($"Add:{phase}"));

    /// <summary>Theseus bootstrap, with git-22 in this state, open in its tab beside BuildYourMarket's.</summary>
    public ProjectViewModel Theseus(SessionLifecycle git22 = SessionLifecycle.Idle, ProjectViewModel? previous = null,
        bool canStart = true, ProjectViewActions? actions = null)
    {
        var reading = WriteTheseus(_folder);
        IReadOnlyList<PieceCard> cards = [Card(Git22, git22) with { CanStart = canStart }];
        var pieces = PiecesOfWork.Build(reading, _ => null, cards, []);

        return ProjectViewModel.Build(reading, pieces, LinksOn([], cards), Tabs(TheseusProject), Settings, actions ?? Actions, null,
            Edits, previous);
    }

    /// <summary>BuildYourMarket MCP with its fifteen links, and no session unless cards are given.</summary>
    public ProjectViewModel BuildYourMarket(ProjectViewModel? previous = null, ProjectViewActions? actions = null,
        IReadOnlyList<PieceCard>? cards = null)
    {
        var links = BuildYourMarketLinks();
        cards ??= BuildYourMarketBoard();
        var pieces = BuildYourMarketPieces(_folder, cards, links);
        var reading = Read(_folder, ProjectViewFixtures.BuildYourMarket);

        return ProjectViewModel.Build(reading, pieces, LinksOn(links, cards), Tabs(ProjectViewFixtures.BuildYourMarket), Settings,
            actions ?? Actions, null, Edits, previous);
    }

    /// <summary>The one-piece project of <see cref="ProjectViewFixtures.WriteOne"/>, its card in this state.</summary>
    public ProjectViewModel One(string pipeline, IReadOnlyDictionary<string, string>? records = null,
        SessionLifecycle lifecycle = SessionLifecycle.NotStarted, ProjectViewModel? previous = null)
    {
        var reading = WriteOne(_folder, pipeline, records);
        IReadOnlyList<PieceCard> cards = [Card(ACard, lifecycle)];
        var pieces = PiecesOfWork.Build(reading, _ => null, cards, []);

        return ProjectViewModel.Build(reading, pieces, LinksOn([], cards), Tabs(OneProject), Settings, Actions, null, Edits, previous);
    }

    /// <summary>A view of any readable project, its pieces built against these cards and links.</summary>
    public ProjectViewModel Of(ProjectFileReading reading, IReadOnlyList<PieceCard> cards, IReadOnlyList<Link>? links = null,
        ProjectViewModel? previous = null)
    {
        var pieces = PiecesOfWork.Build(reading, _ => null, cards, links ?? []);

        return ProjectViewModel.Build(reading, pieces, LinksOn(links ?? [], cards), Tabs(reading.Name), Settings, Actions, null,
            Edits, previous);
    }

    /// <summary>One tab per project in the folder that reads, this one open.</summary>
    public IReadOnlyList<ProjectTabViewModel> Tabs(string open) =>
        ProjectTabViewModel.For(ProjectCatalog.Read(_folder.Folder), open, _ => null);
}
