# Research: objective 1.1, the project view

**Status:** research written overnight on 2026-11-19, from a read-only map of the Theseus code at `main` (1c5d59e).
It is the `research` stage of the `feature` pipeline for objective 1.1, kept in the notes folder because no branch is
named for it yet; it moves to `work/<slug>/research.md` when Nigel names the branch. Nothing here is decided.

The shape Nigel proposed (2026-11-19, recorded in the handoff): one **project card** on the main board, showing the
phase, progress against its exit gate and a roll-up of anything needing him; a **drill-down to a whole-screen
project view**, with a tree on the left (phases, objectives, workstreams) and the pipeline on the right (objectives as
rows, stages as columns, cards at their stage, gates between columns, links drawn); and **one Windows Terminal window
per project**.

## What exists today

| Need | Today | Where |
|---|---|---|
| A grid of cards by column and row | Yes: columns by stage or by session state; rows by area, folder ("Project", which is only the working folder's name), chain or none | `BoardViewModel.cs:40-310`, `BoardView.cs`, `BoardColumnsBy`, `BoardRowsBy` |
| A view that replaces the board | **No.** No page, frame or view switching; other views are separate windows. The main area is one grid holding the board and the drawer laid over it | `MainWindow.xaml:1971-2563` |
| A card's pipeline stage | **No.** A card has only its lane (free text from settings). Which `work/<slug>/` files exist implies the stage, but nothing reads them | `Workstream.cs:14`, `WorkstreamAssembler.cs:51` |
| Reading method files | **No.** Nothing reads `method/`, `projects/` or `work/`. The only parser is a small frontmatter reader that cannot read the pipeline YAML; Core has no YAML or markdown package | `Instructions/Frontmatter.cs` |
| "Needs you" | Two separate things: a session waiting for input (`NeedsAttention`), and reports waiting for confirmation (`Chains.NeedsYou`). The toolbar already sums proposals, reports and ready cards | `SessionActivity.cs:19`, `ChainsViewModel.cs:88-132` |
| A tree | The Chains panel's tree, now with its own row template (links-tree-scrollbar). Its node type is card-only; a project tree needs a node with a kind (phase, objective, workstream) | `MainWindow.xaml:1584`, `ChainNodeViewModel.cs` |
| Lines between cards | **None anywhere**, by design ("never lines drawn between rows") | `ChainNodeViewModel.cs:6-7` |
| A window per group of cards | Yes, by name: a card's window is its own override, else its area's window, else its area, else its column (`theseus-<name>`). Raising a window by name is not possible (nothing maps a window name to a window handle); focusing works through a live session's tab | `WindowGroupName.cs:22-46`, `LaunchCoordinator.cs:150`, `TerminalFocus.cs:39` |

## Decisions the spec needs (each with a recommendation)

1. **Where a card's project and stage come from.**
   - **Project:** the project file's Workstreams table names cards by key (recommended: read it; no new card field).
     The alternative, a project field on the card, duplicates the project file.
   - **Stage:** recommended from the artefacts in `work/<slug>/` (spec.md present means spec reached, and so on),
     plus the result block objective 1.1 already defines for `evidence.md` (method-v0.2). A stage field set by
     sessions would be a claim, not a fact.
2. **Parsing.** The project files are "prose with a fixed shape", and today's file departs from that shape in places
   (objectives as bullets, two different workstream tables). Recommended: tighten `method/project.md`'s shape as part
   of method-v0.2 and write a small reader for it, with a test per shape; read pipeline stage names from the YAML with
   a small reader too, or add a YAML package (Core has only Serilog today; a package is simpler, a hand reader keeps
   Core free of dependencies; Nigel's call).
3. **The view.** Recommended: a second view in the same main grid, switched with the board (the project card's
   "open" and a breadcrumb back), not a separate window, so the drawer, panels and status bar stay.
4. **The roll-up on the project card.** Recommended: sessions waiting for input, reports waiting for confirmation and
   proposals pending, for the project's cards only, plus gates waiting on the owner once stages are read.
5. **Links drawn.** The board draws no lines anywhere today. Recommended for a first version: show links as they are
   shown today (lines of text under a card and highlighting on hover), and draw nothing, until the view proves itself.
6. **The window per project.** Recommended: a new first step in `WindowGroupName.For` for project cards
   (`theseus-<project>`), below an explicit per-card override; "bring the window forward" works through one of the
   project's live sessions, as Focus does today. Ad hoc cards unchanged.

## Risks

- **The project file format is not yet regular** enough to read reliably: the reader and the format have to be
  settled together.
- **Main window size:** `MainWindow.xaml` is about 2,560 lines; the project view should be its own user control.
- **Stage from artefacts can be wrong** (a spec written and then abandoned). The view should show where the stage
  came from.

## Holdout scenarios that apply

None of the three today. A fourth ("a project's cards open in its own window and resume there tomorrow") is a
candidate for Nigel to write as author.
