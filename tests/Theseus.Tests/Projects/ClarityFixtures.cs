using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// 1.1f's two fixtures, the projects as read on 2026-11-21 19:20 and encoded in the mockup's <c>P.bym</c> and
/// <c>P.theseus</c>: BuildYourMarket MCP with a Rounds table added as in spec section B, and Theseus unchanged, with no
/// Rounds table. Card keys are short; card names are the mockup's.
/// </summary>
internal static class ClarityFixtures
{
    public const string Bym = "bym";

    public const string Theseus = "theseus";

    public const string SpecCard = "spec";

    public const string Spec1 = "buildyourmarket-mcp-server-spec";

    public const string Spec2 = "bym-mcp-gui-parity-spec";

    private const string Open = "Prose.\n\n```theseus-evidence\n{ \"result\": \"open\" }\n```\n";

    private const string Pass = "Prose.\n\n```theseus-evidence\n{ \"result\": \"pass\" }\n```\n";

    private static readonly (string Key, string Name)[] BymCards =
    [
        (SpecCard, "BuildYourMarket MCP Server - Spec"),
        ("w0", "BYM MCP W0 - Preconditions inventory"),
        ("w1", "BYM MCP W1 - IdentityServer resources and staff client"),
        ("w2", "BYM MCP W2 - Config P5 refusal"),
        ("w3", "BYM MCP W3 - Build read API"),
        ("w4", "BYM MCP W4 - MCP host and staff plugin"),
        ("w5", "BYM MCP W5 - Infra, Hygiene scripts and pipelines"),
        ("isf", "IdentityServer client API and CLI fixes"),
        ("e1", "IdentityServer redirect URI anchoring (E1)"),
        ("w6", "BYM MCP W6 - GUI parity, table-backed reads"),
        ("w7", "BYM MCP W7 - GUI parity, search-backed reads"),
        ("w8", "BYM MCP W8 - Search and blob read access (option A)"),
    ];

    private static readonly string[] AcceptedSpecs = ["bym-mcp-identity", "bym-mcp-config", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra"];

    /// <summary>The board's cards for BuildYourMarket, W4 and W6 busy and W7 asking, as the mockup has them.</summary>
    public static IReadOnlyList<PieceCard> BymBoard(bool live = true) =>
    [
        .. BymCards.Select(card => new PieceCard(card.Key, card.Name, "#d4683a", live ? card.Key switch
        {
            "w4" or "w6" => SessionLifecycle.Running,
            "w7" => SessionLifecycle.NeedsAttention,
            _ => SessionLifecycle.Idle,
        } : SessionLifecycle.Idle)),
    ];

    /// <summary>
    /// The hand-overs and reports: the spec card's round 1 hand-overs are older than <see cref="Link.ParentPiece"/>, its
    /// round 2 ones record the GUI-parity spec; W0, the client fixes and W8 reported back and were confirmed.
    /// </summary>
    public static IReadOnlyList<Link> BymLinks()
    {
        var links = new List<Link>();
        var confirmed = DateTimeOffset.UnixEpoch.AddDays(3);

        foreach (var child in new[] { "w0", "w1", "w2", "w3", "w4", "w5", "isf" })
        {
            links.Add(NewLink(LinkKind.HandsOver, SpecCard, child));
            var back = NewLink(LinkKind.ReportsBack, child, SpecCard) with { Output = "report.md" };
            links.Add(child is "w0" or "isf" ? back with { SatisfiedUtc = confirmed } : back);
        }

        links.Add(NewLink(LinkKind.HandsOver, "w1", "e1"));

        foreach (var child in new[] { "w6", "w7", "w8" })
        {
            links.Add(NewLink(LinkKind.HandsOver, SpecCard, child) with { ParentPiece = Spec2 });
            var back = NewLink(LinkKind.ReportsBack, child, SpecCard) with { Output = "report.md" };
            links.Add(child == "w8" ? back with { SatisfiedUtc = confirmed } : back);
        }

        return links;
    }

    /// <summary>Writes BuildYourMarket and reads it as the catalog does.</summary>
    public static ProjectFileReading WriteBym(TestProjectsFolder folder)
    {
        folder.Write($"{Bym}/project.md", BymText);
        Records(folder, Bym, Spec1, "research.md", "spec.md", "lessons.md");
        Records(folder, Bym, "bym-mcp-preconditions", "plan.md");

        foreach (var slug in new[] { "bym-mcp-identity", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra" })
        {
            Records(folder, Bym, slug, "spec.md", "plan.md", "implementation.md", "review.md", "lessons.md");
            folder.Write($"{Bym}/work/{slug}/evidence.md", Open);
        }

        Records(folder, Bym, "bym-mcp-config", "spec.md", "plan.md", "implementation.md", "review.md");
        folder.Write($"{Bym}/work/bym-mcp-config/evidence.md", Open);
        Records(folder, Bym, Spec2, "research.md", "plan.md");
        Records(folder, Bym, "bym-mcp-gui-table-reads", "spec.md", "plan.md");
        Records(folder, Bym, "bym-mcp-gui-search-reads", "spec.md");
        Records(folder, Bym, "bym-mcp-search-blob-access", "evidence.md");

        return Readable(ProjectViewFixtures.Read(folder, Bym));
    }

    /// <summary>BuildYourMarket's pieces, the round 1 build specs accepted, the GUI-parity ones not.</summary>
    public static IReadOnlyList<PieceOfWork> BymPieces(TestProjectsFolder folder, bool live = true, StageReports? reports = null)
    {
        var reading = WriteBym(folder);

        return PiecesOfWork.Build(reading, slug => AcceptedSpecs.Contains(slug) && reading.Artefacts[slug].SpecHash is { } hash
                ? new SpecAcceptance(Bym, slug, hash, DateTimeOffset.UnixEpoch)
                : null,
            BymBoard(live), BymLinks(), reports: reports);
    }

    /// <summary>BuildYourMarket's tree, by round.</summary>
    public static PieceTree BymTree(TestProjectsFolder folder)
    {
        var reading = WriteBym(folder);
        var links = BymLinks();
        var pieces = BymPieces(folder);

        return PieceTree.Build(pieces, ProjectViewFixtures.LinksOn(links, BymBoard()), reading.File!.Rounds);
    }

    /// <summary>Theseus's cards: git-22 idle, 1.1e and 1.1f busy.</summary>
    public static IReadOnlyList<PieceCard> TheseusBoard() =>
    [
        new("git-22", "Theseus", "#2f80ed", SessionLifecycle.Idle),
        new("e", "Theseus 1.1e: projects in use", "#2f80ed", SessionLifecycle.Running),
        new("f", "Theseus 1.1f: the project view made obvious (design)", "#2f80ed", SessionLifecycle.Running),
    ];

    /// <summary>Writes Theseus and reads it as the catalog does.</summary>
    public static ProjectFileReading WriteTheseus(TestProjectsFolder folder)
    {
        folder.Write($"{Theseus}/project.md", TheseusText);

        foreach (var slug in new[] { "stop-timing", "card-branches", "parked-cores" })
        {
            Records(folder, Theseus, slug, "research.md", "spec.md", "plan.md", "implementation.md", "evidence.md", "review.md", "lessons.md");
        }

        Records(folder, Theseus, "readme-runtime", "reproduction.md", "evidence.md");
        folder.Write($"{Theseus}/work/readme-runtime/fix.md", "# Fix\n\n## Cause\nWhy.\n");
        Records(folder, Theseus, "guard-mentions", "reproduction.md", "evidence.md");
        folder.Write($"{Theseus}/work/guard-mentions/fix.md", "# Fix\n\n## Spec\nThe rule.\n\n## Cause\nWhy.\n");
        Records(folder, Theseus, "resumed-stop", "reproduction.md", "evidence.md", "review.md", "lessons.md");
        folder.Write($"{Theseus}/work/resumed-stop/fix.md", "# Fix\n\n## Spec\nThe rule.\n\n## Cause\nWhy.\n");
        Records(folder, Theseus, "verifier-card", "research.md", "spec.md", "plan.md", "implementation.md", "review.md", "lessons.md");
        Records(folder, Theseus, "project-view", "research.md", "spec.md", "plan.md", "implementation.md", "evidence.md", "review.md");

        foreach (var slug in new[] { "project-home-folders", "projects-and-method-ui", "project-view-redesign" })
        {
            Records(folder, Theseus, slug, "spec.md", "plan.md", "implementation.md", "review.md");
            folder.Write($"{Theseus}/work/{slug}/evidence.md", Open);
        }

        Records(folder, Theseus, "projects-in-use", "spec.md", "implementation.md");
        Records(folder, Theseus, "project-view-clarity", "spec.md", "concepts.md");

        return Readable(ProjectViewFixtures.Read(folder, Theseus));
    }

    /// <summary>Theseus's pieces, every spec of phase 1 accepted.</summary>
    public static IReadOnlyList<PieceOfWork> TheseusPieces(TestProjectsFolder folder)
    {
        var reading = WriteTheseus(folder);

        return PiecesOfWork.Build(reading, slug => reading.Artefacts[slug].SpecHash is { } hash
            ? new SpecAcceptance(Theseus, slug, hash, DateTimeOffset.UnixEpoch)
            : null, TheseusBoard(), []);
    }

    private static ProjectFileReading Readable(ProjectFileReading reading) =>
        reading.IsReadable ? reading : throw new InvalidOperationException($"the fixture does not read: {reading.Reason}");

    public static PieceOfWork Named(IEnumerable<PieceOfWork> pieces, string name) => pieces.Single(piece => piece.Name == name);

    private static Link NewLink(LinkKind kind, string from, string to) => ProjectViewFixtures.NewLink(kind, from, to);

    private static void Records(TestProjectsFolder folder, string project, string slug, params string[] files)
    {
        foreach (var file in files)
        {
            folder.Write($"{project}/work/{slug}/{file}", file == "evidence.md" ? Pass : $"# {file}\n");
        }
    }

    private static string Objective(string number, string title, string pipeline, string criteria, params (string Card, string Slug, string Branch)[] rows) =>
        $"### Objective {number}: {title}\n{criteria}\n**Pipeline:** {pipeline}\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
        + string.Concat(rows.Select(row => $"| {row.Card} | {row.Slug} | {row.Branch} | a-repo |\n")) + "\n";

    private static string Round2(string objective) => objective.Insert(objective.IndexOf('\n', StringComparison.Ordinal) + 1, "**Round:** 2\n");

    private static string BymText =>
        TestProjectsFolder.ProjectText("BuildYourMarket MCP server", "owner/bym-mcp", header: $"**Before method-v0.2:** {Spec1}\n",
            body: "## Rounds\n\n| Round | Name | Began | Why | By |\n|---|---|---|---|---|\n"
                  + "| 1 | Specify, then build release 2a | 2026-11-18 | The first requirements | Nigel |\n"
                  + "| 2 | GUI parity | 2026-11-21 | The tool catalogue was built from support tickets, not from what the GUI offers | Nigel |\n\n"
                  + "## Phase 0: Specify\n\n**Exit gate:** Nigel approves the spec.\n\n"
                  + Objective("0.1", "Specify the BuildYourMarket MCP server", "feature", "- [x] The spec.", (SpecCard, Spec1, "none"))
                  + "## Phase 1: Build read-only, internal\n\n"
                  + Objective("1.1", "Preconditions", "freeform", "- [ ] P4 inventory recorded.", ("w0", "bym-mcp-preconditions", "none"))
                  + Objective("1.2", "Identity and Config", "feature", "- [ ] Two ApiResources.",
                      ("w1", "bym-mcp-identity", "owner/bym-mcp"), ("w2", "bym-mcp-config", "owner/w2"),
                      ("isf", "identityserver-client-api-and-cli-fixes", "owner/isf"))
                  + Objective("1.3", "BuildYourMarket.Api, release 2a", "feature", "- [ ] The contract is frozen.",
                      ("w3", "bym-mcp-read-api", "owner/w3"))
                  + Objective("1.4", "BuildYourMarket.Mcp host and deployment", "feature", "- [ ] 18 tools.",
                      ("w4", "bym-mcp-host", "owner/w4"), ("w5", "bym-mcp-infra", "owner/w5"))
                  + Objective("1.5", "Data protection", "freeform", "- [ ] D7 and P6 recorded by the DPO.")
                  + Objective("1.6", "Staff pilot", "freeform", "- [ ] Pilot measure agreed.")
                  + Round2(Objective("1.7", "GUI parity, table-backed reads", "feature", "- [ ] Tools 4.9 to 4.21 built.",
                      ("w6", "bym-mcp-gui-table-reads", "owner/w6")))
                  + Round2(Objective("1.8", "GUI parity, search-backed reads", "feature", "- [ ] Tools 4.1 to 4.8 built.",
                      ("w7", "bym-mcp-gui-search-reads", "owner/w7")))
                  + Round2(Objective("1.9", "Search, table and blob read access (option A, D12)", "freeform", "- [ ] RBAC on search.",
                      ("w8", "bym-mcp-search-blob-access", "owner/w8")))
                  + Round2(Objective("1.10", "Specify the GUI-parity tools", "feature", "- [x] research.md.\n- [ ] Nigel accepts the plan.",
                      (SpecCard, Spec2, "none")))
                  + "## Phase 2: Customer release\n\n"
                  + "## Decisions\n\n| Date | Decision | By |\n|---|---|---|\n| 2026-11-18 | Project home chosen | Claude |\n");

    private static string TheseusText =>
        TestProjectsFolder.ProjectText("Theseus", "owner/theseus",
            header: "**Before method-v0.2:** method-kit, holdout-scenarios, readme-runtime, guard-mentions, stop-timing, "
                    + "card-branches, parked-cores, resumed-stop, verifier-card\n",
            body: "All run on main, one after another (chosen 2026-11-18); accepted 2026-11-19.\n\n"
                  + "## Phase 0: Seed the method\n\n"
                  + Objective("0.1", "Method kit v0", "freeform", "- [ ] AGENTS.md exists.", ("git-22", "method-kit", "main"))
                  + Objective("0.2", "Holdout scenarios", "freeform", "- [ ] Three scenarios.", ("git-22", "holdout-scenarios", "[not recorded]"))
                  + Objective("0.3", "Five changes through the method by hand", "feature or bug", "- [x] Five changes.",
                      ("git-22", "readme-runtime", "main"), ("git-22", "guard-mentions", "main"), ("git-22", "stop-timing", "main"),
                      ("git-22", "card-branches", "main"), ("git-22", "parked-cores", "main"), ("git-22", "resumed-stop", "main"))
                  + "## Phase 1: Evidence\n\n"
                  + Objective("1.0", "A verifier card", "feature", "- [ ] One action.", ("git-22", "verifier-card", "nigel/verifier-card"))
                  + Objective("1.1", "Checks shown on cards", "feature", "- [ ] A result block.",
                      ("git-22", "project-view", "nigel/project-view"), ("git-22", "project-home-folders", "nigel/project-home-folders"),
                      ("git-22", "projects-and-method-ui", "nigel/projects-and-method-ui"),
                      ("git-22", "project-view-redesign", "nigel/project-view-redesign"),
                      ("e", "projects-in-use", "nigel/projects-in-use"), ("f", "project-view-clarity", "none")));
}
