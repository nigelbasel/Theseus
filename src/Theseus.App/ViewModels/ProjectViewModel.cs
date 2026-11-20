using System.Globalization;
using System.Windows.Input;
using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The whole-screen view of one project (project-view AC 14 to 16): the tree of phases, objectives and workstreams on
/// the left; on the right a row per objective with workstreams across the stage columns, with the gates marked and the
/// specs waiting on the owner actionable. It does not fold, and cards are not dragged in it: a stage comes from the
/// artefacts.
/// </summary>
public sealed class ProjectViewModel
{
    /// <summary>The columns, in order (AC 6).</summary>
    public static IReadOnlyList<PipelineStage> Stages { get; } =
    [
        PipelineStage.Research, PipelineStage.Spec, PipelineStage.Plan, PipelineStage.Implement,
        PipelineStage.Verify, PipelineStage.Review, PipelineStage.Compound,
    ];

    /// <summary>The column headings, with a bug's stage under each feature stage it shares a column with (AC 6).</summary>
    public static IReadOnlyList<string> ColumnHeadings { get; } =
        ["Research · reproduce", "Spec · rule", "Plan", "Implement · fix", "Verify", "Review", "Compound"];

    private ProjectViewModel(ProjectFile file)
    {
        Name = file.Name;
        Title = file.Title;
    }

    /// <summary>The project's name.</summary>
    public string Name { get; }

    /// <summary>Its title.</summary>
    public string Title { get; }

    /// <summary>The current phase and its exit gate.</summary>
    public string PhaseText { get; private init; } = string.Empty;

    /// <summary>The tree (AC 15).</summary>
    public IReadOnlyList<ProjectTreeNodeViewModel> Tree { get; private init; } = [];

    /// <summary>The pipeline's rows, then the groups at no stage (AC 4, 15).</summary>
    public IReadOnlyList<ProjectPipelineRowViewModel> Rows { get; private init; } = [];

    /// <summary>Every card in the view, in reading order, for Shift+click (AC 15).</summary>
    public IReadOnlyList<WorkstreamCardViewModel> CardsInOrder { get; private init; } = [];

    /// <summary>The specs waiting on the owner, for the menu bar's Open spec and Accept spec (R16).</summary>
    public IReadOnlyList<SpecAtGate> WaitingSpecs { get; private init; } = [];


    /// <summary>The primary's pull request into the trunk, which has no Accept (AC 7).</summary>
    public string MainPullRequestText => "Pull request into the trunk: not read yet (objective 1.2 reads it once built)";

    /// <summary>What the view shows, as text, so an unchanged view is not rebuilt (and its tree and scroll kept).</summary>
    public string Signature { get; private init; } = string.Empty;

    /// <summary>Back to the board, from the breadcrumb (AC 14).</summary>
    public ICommand? BackCommand { get; init; }

    /// <summary>Builds the view of a readable project.</summary>
    /// <param name="reading">The project, with its slugs' artefacts read in the background.</param>
    /// <param name="cards">Every card on the board; the project's own are picked out.</param>
    /// <param name="acceptance">The owner's latest acceptance of a slug's spec, from Theseus's store.</param>
    /// <param name="hasEnded">Whether a verifier card's job is over.</param>
    /// <param name="openSpec">Opens a spec as read.</param>
    /// <param name="acceptSpec">Accepts a spec, after asking.</param>
    /// <param name="back">Closes the view.</param>
    public static ProjectViewModel Build(ProjectFileReading reading, IReadOnlyList<WorkstreamCardViewModel> cards,
        Func<string, string, SpecAcceptance?> acceptance, Func<Workstream, bool> hasEnded,
        Action<SpecAtGate> openSpec, Action<SpecAtGate> acceptSpec, ICommand? back)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(cards);

        var file = reading.File ?? throw new ArgumentException("Only a readable project has a view.", nameof(reading));
        var byKey = cards.ToDictionary(card => card.Key, StringComparer.OrdinalIgnoreCase);
        var stages = new Dictionary<string, WorkstreamStage>(StringComparer.OrdinalIgnoreCase);
        var specs = new Dictionary<string, SpecAtGate?>(StringComparer.OrdinalIgnoreCase);

        foreach (var objective in file.Objectives)
        {
            foreach (var row in objective.Workstreams)
            {
                var artefacts = reading.Artefacts.GetValueOrDefault(row.Slug) ?? WorkstreamArtefacts.NotStarted(row.Slug);
                var stage = StageCalculator.Calculate(objective.Pipelines, artefacts, acceptance(file.Name, row.Slug));

                stages[row.Slug] = stage;
                specs[row.Slug] = SpecAtGate.For(file.Name, row.Slug, stage.Pipeline, artefacts);
            }
        }

        var layout = ProjectLayout.Build(file, [.. cards.Select(card => card.Workstream)], (_, row) => stages[row.Slug], hasEnded);
        var rows = new List<ProjectPipelineRowViewModel>();
        var tree = new List<ProjectTreeNodeViewModel>();
        var rowOf = new Dictionary<string, ProjectPipelineRowViewModel>(StringComparer.Ordinal);
        var waiting = new List<SpecAtGate>();

        ProjectEntryViewModel Entry(SlugEntry entry, WorkstreamCardViewModel? card)
        {
            var stage = entry.Stage;
            var spec = specs.GetValueOrDefault(entry.Row.Slug);
            var isWaiting = stage.IsWaitingOnOwner && spec is not null;

            if (card is not null)
            {
                card.ProjectSource = stage.Source.Describe;
            }

            return new ProjectEntryViewModel(entry.Row.Slug, $"{entry.Row.Slug} · {entry.Row.Workstream}", card, stage, false)
            {
                Note = Note(entry, card is not null),
                Detail = Detail(stage),
                IsWaitingOnYou = isWaiting,
                WaitingText = stage.GateAfter(PipelineStage.Spec)?.State == GateState.ChangedSinceAccepted
                    ? "Changed since accepted: waiting on you"
                    : "Waiting on you",
                OpenSpecCommand = spec is null ? null : new RelayCommand(_ => openSpec(spec)),
                AcceptSpecCommand = isWaiting ? new RelayCommand(_ => acceptSpec(spec!)) : null,
            };
        }

        foreach (var objectiveLayout in layout.Objectives.Where(each => each.Entries.Count > 0))
        {
            var cells = Stages.ToDictionary(stage => stage, _ => new List<ProjectEntryViewModel>());

            foreach (var entry in objectiveLayout.Entries)
            {
                var column = entry.Stage.Position ?? PipelineStage.Research;
                var holders = entry.CardKeys.Select(key => byKey.GetValueOrDefault(key)).OfType<WorkstreamCardViewModel>().ToList();

                if (entry.Stage.Pipeline is null or PipelineKind.Freeform && holders.Count == 0)
                {
                    continue;
                }

                if (holders.Count == 0)
                {
                    cells[column].Add(Entry(entry, null));
                }

                foreach (var holder in holders)
                {
                    cells[column].Add(Entry(entry, holder));
                }

                foreach (var verifier in entry.VerifierKeys.Select(key => byKey.GetValueOrDefault(key)).OfType<WorkstreamCardViewModel>())
                {
                    verifier.ProjectSource = entry.Stage.Source.Describe;
                    cells[SlugEntry.VerifierStage].Add(new ProjectEntryViewModel(entry.Row.Slug, entry.Row.Slug, verifier, entry.Stage, true)
                    {
                        Note = $"verifying {entry.Row.Slug}",
                        Detail = Detail(entry.Stage),
                    });
                }

                if (entry.Stage.IsWaitingOnOwner && specs.GetValueOrDefault(entry.Row.Slug) is { } spec)
                {
                    waiting.Add(spec);
                }
            }

            var objective = objectiveLayout.Objective;
            var pipelineRow = new ProjectPipelineRowViewModel($"{objective.Number} {objective.Title}",
                [.. Stages.Select(stage => new ProjectStageCellViewModel(stage, cells[stage]))], []);

            rows.Add(pipelineRow);
            rowOf[objective.Number] = pipelineRow;
        }

        AddGroup(rows, "No slug yet", layout.NoSlugYet, byKey);
        AddGroup(rows, "Not in the project file", layout.NotInProjectFile, byKey);

        foreach (var phase in file.Phases)
        {
            var objectives = phase.Objectives.Select(objective =>
            {
                var row = rowOf.GetValueOrDefault(objective.Number);
                var slugs = objective.Workstreams.Select(each =>
                {
                    var stage = stages[each.Slug];
                    var waits = stage.IsWaitingOnOwner;

                    return new ProjectTreeNodeViewModel($"{each.Slug} ({each.Workstream})",
                        Where(stage, waits), [], row, waits);
                }).ToList();

                var ticked = objective.IsDone
                    ? "done"
                    : $"{objective.TickedCount} of {objective.Criteria.Count} ticked";

                return new ProjectTreeNodeViewModel($"{objective.Number} {objective.Title}", ticked, slugs, row,
                    slugs.Any(slug => slug.IsWaitingOnYou));
            }).ToList();

            var current = ReferenceEquals(phase, file.CurrentPhase) ? "current" : phase.HasOpenObjective ? string.Empty : "done";

            tree.Add(new ProjectTreeNodeViewModel($"Phase {phase.Number}: {phase.Name}", current, objectives,
                objectives.Select(node => node.Row).FirstOrDefault(row => row is not null),
                objectives.Any(node => node.IsWaitingOnYou)));
        }

        foreach (var group in rows.Where(row => row.IsGroup))
        {
            tree.Add(new ProjectTreeNodeViewModel(group.Heading, $"{group.Loose.Count} card(s)", [], group, false));
        }

        var phaseText = file.CurrentPhase is { } currentPhase
            ? $"Phase {currentPhase.Number}: {currentPhase.Name}. Exit gate: {(currentPhase.ExitGate.Length > 0 ? currentPhase.ExitGate : "[not written]")}"
            : "Every objective is done";

        var inOrder = rows
            .SelectMany(row => row.IsGroup ? row.Loose : row.Cells.SelectMany(cell => cell.Entries))
            .Select(entry => entry.Card)
            .OfType<WorkstreamCardViewModel>()
            .Distinct()
            .ToList();

        return new ProjectViewModel(file)
        {
            PhaseText = phaseText,
            Tree = tree,
            Rows = rows,
            CardsInOrder = inOrder,
            WaitingSpecs = waiting,
            BackCommand = back,
            Signature = SignatureOf(file, rows, tree),
        };
    }

    private static void AddGroup(List<ProjectPipelineRowViewModel> rows, string heading, IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, WorkstreamCardViewModel> byKey)
    {
        var cards = keys.Select(key => byKey.GetValueOrDefault(key)).OfType<WorkstreamCardViewModel>().ToList();

        if (cards.Count == 0)
        {
            return;
        }

        rows.Add(new ProjectPipelineRowViewModel(heading, [],
        [
            .. cards.Select(card => new ProjectEntryViewModel(card.Workstream.Slug ?? string.Empty, card.Title, card,
                StageCalculator.Calculate([], WorkstreamArtefacts.NotStarted(string.Empty), null), false)
            {
                Note = card.Workstream.Slug is { } slug ? $"{slug} is not in the project file" : "no slug yet",
            }),
        ]));
    }

    private static string Note(SlugEntry entry, bool hasCard)
    {
        var stage = entry.Stage;

        if (stage.Note is { } note)
        {
            return note;
        }

        if (!stage.HasStarted)
        {
            return hasCard ? "nothing written yet" : entry.IsEarlierSlug ? "earlier slug, nothing written" : "not started";
        }

        var verify = stage.GateAfter(PipelineStage.Verify);

        if (stage.Position >= PipelineStage.Verify && verify?.Note is { } evidence)
        {
            return evidence;
        }

        return entry.IsEarlierSlug ? "earlier slug" : hasCard ? string.Empty : "no card";
    }

    private static string Detail(WorkstreamStage stage)
    {
        var passed = stage.Gates.Where(gate => gate.State == GateState.Passed).Select(gate => StageName(gate.Stage)).ToList();
        var lines = new List<string>
        {
            stage.Gates.Count == 0 ? "No gate marks" : passed.Count == 0 ? "No gate passed yet" : $"Passed: {string.Join(", ", passed)}",
        };

        if (stage.EvidenceClaims.Count > 0)
        {
            lines.Add("The evidence block claims (information only): "
                      + string.Join(", ", stage.EvidenceClaims.Select(claim => $"{claim.Gate} {claim.Accepted ?? "not accepted"}")));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Where(WorkstreamStage stage, bool waits)
    {
        if (stage.Pipeline is null or PipelineKind.Freeform)
        {
            return stage.Pipeline is null ? "no pipeline chosen" : "freeform";
        }

        var at = stage.Position is { } position ? $"at {StageName(position)}" : stage.Note ?? "not started";
        var passed = stage.Gates.Count(gate => gate.State == GateState.Passed);

        return string.Join(" · ", new[]
        {
            at,
            stage.Gates.Count > 0 ? $"{passed} gate(s) passed" : stage.Note ?? string.Empty,
            waits ? "Waiting on you" : string.Empty,
        }.Where(part => part.Length > 0).Distinct());
    }

    private static string StageName(PipelineStage stage) => stage.ToString().ToLower(CultureInfo.InvariantCulture);

    /// <summary>
    /// Takes the read sources from a newer build of the same view, which has the same signature and so the same entries
    /// in the same order: a new commit updates the tooltips in place, without a rebuild (PR 2 review, item 4).
    /// </summary>
    public void TakeSourcesFrom(ProjectViewModel newer)
    {
        ArgumentNullException.ThrowIfNull(newer);

        foreach (var (mine, theirs) in Entries(this).Zip(Entries(newer)))
        {
            mine.Source = theirs.Source;
        }
    }

    private static IEnumerable<ProjectEntryViewModel> Entries(ProjectViewModel view) =>
        view.Rows.SelectMany(row => row.Loose.Concat(row.Cells.SelectMany(cell => cell.Entries)));

    // The signature leaves out where each slug was read: a new commit is not a new view.
    private static string SignatureOf(ProjectFile file, IReadOnlyList<ProjectPipelineRowViewModel> rows, IReadOnlyList<ProjectTreeNodeViewModel> tree) =>
        string.Join("|", new[] { file.Name, file.Title }
            .Concat(rows.Select(row => row.Heading + ":" + string.Join(",", row.Loose.Select(Shape))
                                       + ":" + string.Join(";", row.Cells.Select(cell => string.Join(",", cell.Entries.Select(Shape))))))
            .Concat(Flatten(tree).Select(node => $"{node.Text}={node.Detail}")));

    private static string Shape(ProjectEntryViewModel entry) =>
        $"{entry.Slug}/{entry.Card?.Key}/{entry.Note}/{entry.IsWaitingOnYou}/{entry.WaitingText}/{entry.Detail}";

    private static IEnumerable<ProjectTreeNodeViewModel> Flatten(IEnumerable<ProjectTreeNodeViewModel> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));
}
