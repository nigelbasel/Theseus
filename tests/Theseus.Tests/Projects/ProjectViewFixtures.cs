using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The accepted mockup's two projects (1.1d PR 1) as project homes in a <see cref="TestProjectsFolder"/>: Theseus
/// bootstrap with git-22 on every row, and BuildYourMarket MCP converted to the method-v0.2 shape with its fifteen
/// links. Theseus is the mockup's twelve pieces; the summary and tree criteria count twelve.
/// </summary>
internal static class ProjectViewFixtures
{
    public const string TheseusProject = "theseus";

    public const string BuildYourMarket = "buildyourmarket-mcp";

    public const string Git22 = "git-22";

    public const string ServerSpec = "buildyourmarket-mcp-server-spec";

    public const string ClientFixes = "identityserver-client-api-and-cli-fixes";

    public const string RedirectAnchoring = "identityserver-redirect-uri-anchoring-e1";

    public const string OpenEvidence = "Prose.\n\n```theseus-evidence\n{ \"result\": \"open\" }\n```\n";

    public const string FailedEvidence = "Prose.\n\n```theseus-evidence\n{ \"result\": \"fail\" }\n```\n";

    public const string PassingEvidence = "Prose.\n\n```theseus-evidence\n{ \"result\": \"pass\" }\n```\n";

    private const string NoBlock = "Prose with no result block.\n";

    private const string Header = "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n";

    private static readonly string[] AllFeatureFiles =
        ["research.md", "spec.md", "plan.md", "implementation.md", "evidence.md", "review.md", "lessons.md"];

    /// <summary>The BuildYourMarket cards by key, each named by its piece, in the mockup's order.</summary>
    public static readonly (string Card, string Piece)[] BuildYourMarketCards =
    [
        (ServerSpec, ServerSpec),
        ("bym-mcp-w0-preconditions-inventory", "bym-mcp-preconditions"),
        ("bym-mcp-w1-identityserver-resources-and-staff-client", "bym-mcp-identity"),
        ("bym-mcp-w2-config-p5-refusal", "bym-mcp-config"),
        ("bym-mcp-w3-build-read-api", "bym-mcp-read-api"),
        ("bym-mcp-w4-mcp-host-and-staff-plugin", "bym-mcp-host"),
        ("bym-mcp-w5-infra-hygiene-scripts-and-pipelines", "bym-mcp-infra"),
        (ClientFixes, ClientFixes),
        (RedirectAnchoring, RedirectAnchoring),
    ];

    private static readonly string[] AcceptedBuildYourMarketSpecs =
        ["bym-mcp-identity", "bym-mcp-config", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra"];

    public static PieceCard Card(string key, SessionLifecycle lifecycle = SessionLifecycle.NotStarted, bool blocked = false,
        WorkstreamRole role = WorkstreamRole.None) =>
        new(key, key, "#2f80ed", lifecycle, blocked, role);

    /// <summary>Writes Theseus bootstrap and reads it as the catalog does.</summary>
    public static ProjectFileReading WriteTheseus(TestProjectsFolder folder)
    {
        folder.Write($"{TheseusProject}/project.md", TheseusText);

        foreach (var slug in new[] { "stop-timing", "card-branches", "parked-cores" })
        {
            Records(folder, TheseusProject, slug, AllFeatureFiles);
        }

        Records(folder, TheseusProject, "readme-runtime", "reproduction.md", "evidence.md");
        folder.Write($"{TheseusProject}/work/readme-runtime/fix.md", "# Fix\n\n## Cause\nWhy.\n");
        Records(folder, TheseusProject, "guard-mentions", "reproduction.md", "evidence.md");
        folder.Write($"{TheseusProject}/work/guard-mentions/fix.md", "# Fix\n\n## Spec\nThe rule.\n\n## Cause\nWhy.\n");
        Records(folder, TheseusProject, "resumed-stop", "reproduction.md", "evidence.md", "review.md", "lessons.md");
        folder.Write($"{TheseusProject}/work/resumed-stop/fix.md", "# Fix\n\n## Spec\nThe rule.\n");
        Records(folder, TheseusProject, "verifier-card", "research.md", "spec.md", "plan.md", "implementation.md", "review.md", "lessons.md");
        Records(folder, TheseusProject, "project-view", "research.md", "spec.md", "plan.md", "implementation.md", "review.md");
        Records(folder, TheseusProject, "project-home-folders", "spec.md", "plan.md", "implementation.md", "review.md");
        Records(folder, TheseusProject, "projects-and-method-ui", "spec.md", "plan.md", "implementation.md", "review.md");

        foreach (var slug in new[] { "project-view", "project-home-folders", "projects-and-method-ui" })
        {
            folder.Write($"{TheseusProject}/work/{slug}/evidence.md", OpenEvidence);
        }

        return Read(folder, TheseusProject);
    }

    /// <summary>Theseus bootstrap's pieces, with git-22 in the given state and no spec accepted.</summary>
    public static IReadOnlyList<PieceOfWork> TheseusPieces(TestProjectsFolder folder,
        SessionLifecycle git22 = SessionLifecycle.Idle, bool blocked = false, StageReports? reports = null) =>
        PiecesOfWork.Build(WriteTheseus(folder), _ => null, [Card(Git22, git22, blocked)], [], reports: reports);

    /// <summary>Reports in which only this card's session has said what it is doing.</summary>
    public static StageReports Said(string card, PipelineStage? stage, string doing) =>
        StageReports.None with
        {
            NowAtOf = key => string.Equals(key, card, StringComparison.OrdinalIgnoreCase)
                ? new SessionStageReport(stage, doing, DateTimeOffset.UnixEpoch)
                : null,
        };

    /// <summary>Writes BuildYourMarket MCP and reads it as the catalog does.</summary>
    public static ProjectFileReading WriteBuildYourMarket(TestProjectsFolder folder)
    {
        folder.Write($"{BuildYourMarket}/project.md", BuildYourMarketText);
        Records(folder, BuildYourMarket, ServerSpec, "research.md", "spec.md", "lessons.md");
        Records(folder, BuildYourMarket, "bym-mcp-preconditions", "plan.md");
        Records(folder, BuildYourMarket, "bym-mcp-config", "spec.md", "plan.md", "implementation.md", "review.md");

        foreach (var slug in new[] { "bym-mcp-identity", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra" })
        {
            Records(folder, BuildYourMarket, slug, "spec.md", "plan.md", "implementation.md", "review.md", "lessons.md");
        }

        foreach (var slug in AcceptedBuildYourMarketSpecs)
        {
            folder.Write($"{BuildYourMarket}/work/{slug}/evidence.md", NoBlock);
        }

        return Read(folder, BuildYourMarket);
    }

    /// <summary>The BuildYourMarket board cards, none with a session.</summary>
    public static IReadOnlyList<PieceCard> BuildYourMarketBoard() =>
        [.. BuildYourMarketCards.Select(entry => Card(entry.Card))];

    /// <summary>The mockup's fifteen links between the BuildYourMarket cards, in its order.</summary>
    public static IReadOnlyList<Link> BuildYourMarketLinks()
    {
        string CardOf(string piece) => BuildYourMarketCards.Single(entry => entry.Piece == piece).Card;

        var reported = DateTimeOffset.UnixEpoch.AddDays(2);
        var links = new List<Link>();

        foreach (var piece in new[] { "bym-mcp-preconditions", "bym-mcp-identity", "bym-mcp-config", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra" })
        {
            links.Add(NewLink(LinkKind.HandsOver, ServerSpec, CardOf(piece)));
            links.Add(NewLink(LinkKind.ReportsBack, CardOf(piece), ServerSpec) with { Output = "report.md", SatisfiedUtc = reported });
        }

        links.Add(NewLink(LinkKind.HandsOver, CardOf("bym-mcp-identity"), RedirectAnchoring));
        links.Add(NewLink(LinkKind.HandsOver, ServerSpec, ClientFixes));
        links.Add(NewLink(LinkKind.ReportsBack, ClientFixes, ServerSpec));

        return links;
    }

    /// <summary>BuildYourMarket MCP's pieces, with the phase 2 specs accepted as they read and no session.</summary>
    public static IReadOnlyList<PieceOfWork> BuildYourMarketPieces(TestProjectsFolder folder, IReadOnlyList<PieceCard>? cards = null,
        IReadOnlyList<Link>? links = null)
    {
        var reading = WriteBuildYourMarket(folder);

        return PiecesOfWork.Build(reading, slug => Accepted(reading, slug), cards ?? BuildYourMarketBoard(),
            links ?? BuildYourMarketLinks());
    }

    public const string OneProject = "one";

    public const string ACard = "a-card";

    public const string APiece = "a-piece";

    /// <summary>
    /// A project of one piece, <see cref="APiece"/>, worked by <see cref="ACard"/> under objective 0.1, with these records
    /// (file name to text) in its folder; no folder when there are none.
    /// </summary>
    public static ProjectFileReading WriteOne(TestProjectsFolder folder, string pipeline,
        IReadOnlyDictionary<string, string>? records = null, bool beforeKit = false, bool objectiveDone = false)
    {
        var objective = Objective("0.1", "Something", pipeline, (ACard, APiece));

        if (objectiveDone)
        {
            objective = objective.Replace("- [ ] A criterion.", "- [x] A criterion.", StringComparison.Ordinal);
        }

        folder.Write($"{OneProject}/project.md", TestProjectsFolder.ProjectText("One", header: beforeKit ? $"**Before method-v0.2:** {APiece}\n" : "",
            body: "## Phase 0: Start\n\n" + objective));

        foreach (var (file, text) in records ?? new Dictionary<string, string>())
        {
            folder.Write($"{OneProject}/work/{APiece}/{file}", text);
        }

        return Read(folder, OneProject);
    }

    /// <summary>The one piece of <see cref="WriteOne"/>, with its card in this state and its spec accepted when asked.</summary>
    public static PieceOfWork OnePiece(TestProjectsFolder folder, string pipeline, IReadOnlyDictionary<string, string>? records = null,
        PieceCard? card = null, bool accepted = false, bool beforeKit = false, bool objectiveDone = false, StageReports? reports = null)
    {
        var reading = WriteOne(folder, pipeline, records, beforeKit, objectiveDone);
        var artefacts = reading.Artefacts[APiece];
        var hash = artefacts.SpecHash ?? artefacts.RuleHash;
        SpecAcceptance? acceptance = accepted && hash is not null ? new SpecAcceptance(OneProject, APiece, hash, DateTimeOffset.UnixEpoch) : null;

        return PiecesOfWork.Build(reading, _ => acceptance, [card ?? Card(ACard)], [], reports: reports).Single();
    }

    /// <summary>Records for <see cref="OnePiece"/>: each named file with a heading, evidence.md passing.</summary>
    public static Dictionary<string, string> Files(params string[] files) =>
        files.ToDictionary(file => file, file => file == "evidence.md" ? PassingEvidence : $"# {file}\n");

    /// <summary>The links read against these cards, none of them live.</summary>
    public static LinkStatus LinksOn(IReadOnlyList<Link> links, IEnumerable<PieceCard> cards) =>
        new(links, cards.Select(card => new LinkCard(card.Key, card.Title, false, card.IsBlocked, card.HasSession, null)));

    public static Link NewLink(LinkKind kind, string from, string to) =>
        new(Guid.NewGuid().ToString("N")[..8], kind, from, to, DateTimeOffset.UnixEpoch, Link.MadeOnTheBoard);

    public static ProjectFileReading Read(TestProjectsFolder folder, string project) =>
        ProjectCatalog.Read(folder.Folder).Single(reading => reading.Name == project);

    public static void Records(TestProjectsFolder folder, string project, string slug, params string[] files)
    {
        foreach (var file in files)
        {
            folder.Write($"{project}/work/{slug}/{file}", file == "evidence.md" ? PassingEvidence : $"# {file}\n");
        }
    }

    private static SpecAcceptance? Accepted(ProjectFileReading reading, string slug) =>
        AcceptedBuildYourMarketSpecs.Contains(slug) && reading.Artefacts[slug].SpecHash is { } hash
            ? new SpecAcceptance(reading.Name, slug, hash, DateTimeOffset.UnixEpoch)
            : null;

    private static string Objective(string number, string title, string pipeline, params (string Workstream, string Slug)[] rows) =>
        $"### Objective {number}: {title}\n- [ ] A criterion.\n\n**Pipeline:** {pipeline}\n{Header}"
        + string.Concat(rows.Select(row => $"| {row.Workstream} | {row.Slug} | owner/{row.Slug} | a-repo |\n")) + "\n";

    private static string TheseusText =>
        TestProjectsFolder.ProjectText("Theseus bootstrap", "owner/theseus",
            header: "**Before method-v0.2:** method-kit, holdout-scenarios, readme-runtime, guard-mentions, stop-timing, "
                    + "card-branches, parked-cores, resumed-stop, verifier-card\n",
            body: "## Phase 0: Seed the method\n\n"
                  + Objective("0.1", "Method kit v0", "freeform", (Git22, "method-kit"))
                  + Objective("0.2", "Holdout scenarios", "freeform", (Git22, "holdout-scenarios"))
                  + Objective("0.3", "Five changes through the method by hand", "feature or bug",
                      (Git22, "readme-runtime"), (Git22, "guard-mentions"), (Git22, "stop-timing"), (Git22, "card-branches"),
                      (Git22, "parked-cores"), (Git22, "resumed-stop"))
                  + "## Phase 1: Evidence\n\n"
                  + Objective("1.0", "A verifier card", "feature", (Git22, "verifier-card"))
                  + Objective("1.1", "Checks shown on cards", "feature",
                      (Git22, "project-view"), (Git22, "project-home-folders"), (Git22, "projects-and-method-ui")));

    // The shape of the real file since its move to method-v0.2 (2026-11-21): phases renumbered from 0, the spec finished
    // under method-v0.1 and listed as such, its objective ticked, two objectives nothing works on yet and an empty last
    // phase. The identityserver-client-api-and-cli-fixes row the real file has since gained is left out, so AC 13's
    // card that no project file lists keeps its case.
    private static string BuildYourMarketText
    {
        get
        {
            (string, string) Row(string piece) => (BuildYourMarketCards.Single(entry => entry.Piece == piece).Card, piece);

            return TestProjectsFolder.ProjectText("BuildYourMarket MCP server", "owner/bym-mcp",
                header: $"**Before method-v0.2:** {ServerSpec}\n",
                body: "## Phase 0: Specify\n\n**Exit gate:** Nigel approves the spec.\n\n"
                      + Objective("0.1", "Specify the BuildYourMarket MCP server", "feature", Row(ServerSpec))
                          .Replace("- [ ] A criterion.", "- [x] A criterion.", StringComparison.Ordinal)
                          .Replace($"| owner/{ServerSpec} |", "| none |", StringComparison.Ordinal)
                      + "## Phase 1: Build read-only, internal\n\n"
                      + Objective("1.1", "Preconditions", "freeform", Row("bym-mcp-preconditions"))
                      + Objective("1.2", "Identity and Config", "feature", Row("bym-mcp-identity"), Row("bym-mcp-config"))
                      + Objective("1.3", "BuildYourMarket.Api, release 2a", "feature", Row("bym-mcp-read-api"))
                      + Objective("1.4", "BuildYourMarket.Mcp host and deployment", "feature", Row("bym-mcp-host"),
                          Row("bym-mcp-infra"))
                      + Objective("1.5", "Data protection", "freeform")
                      + Objective("1.6", "Staff pilot", "freeform")
                      + "## Phase 2: Customer release\n\n");
        }
    }
}
