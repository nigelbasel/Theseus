# Plan: objective 1.1, pull request 1 (the format and reading projects)

Pipeline `feature`, stage **plan**, for `spec.md` ACs 1 to 11 and the `method-v0.2` release. Pull requests 2 (the
project card and view) and 3 (the terminal window) are not in this plan.

## Order

1. **method-v0.2** (no code). `method/project.md` tightened to the shape of AC 3; `feature.yaml` and `bug.yaml` mark
   plan and review as evidence gates inside a project (AC 7, departure 5); the `theseus-evidence` result block
   documented in the method (from the evidence-block proposal, with its answered questions); P18 is already in the
   README's rules, and the README's version moves to `method-v0.2`. `projects/theseus-bootstrap.md` rewritten to the
   new shape with `Kit: method-v0.2` and `Primary branch: nigel/theseus`, every criterion's text and tick kept.
   *Verified by:* the reader's test over the real project file (step 2), and `PublicFilesTests`.
2. **The project file reader** (AC 3, 11), `src/Theseus.Core/Projects/`: `ProjectFileReader` returning a
   `ProjectFileReading` (readable, pinned to method-v0.1, or unreadable with line and reason) over `ProjectFile`,
   `ProjectPhase`, `ProjectObjective`, `ProjectCriterion`, `ProjectWorkstreamRow`, `PipelineKind`.
   *Tests first:* `tests/Theseus.Tests/Projects/ProjectFileReaderTests.cs`, a test per shape and per refusal, and one
   that reads the repository's own `projects/theseus-bootstrap.md`.
3. **Reading with git** (AC 2, 5): `IGitReader` and `GitCli` (runs `git -C <folder> ...`, read-only commands only,
   never fetch); `ProjectCatalog` finds the primary branch from the default branch's copy of each project file and
   reads the file from the primary; `WorkstreamArtefactReader` chooses the source branch for a slug (its own branch
   while unmerged, else the primary, else not on this machine) and reads its `work/<slug>/` artefacts.
   *Tests first:* against real throwaway repositories made in the temp folder (git is on every machine that builds
   Theseus); commits there pass identity with `-c` so no git config is touched.
4. **Stages and gates** (AC 6 to 9): `StageCalculator` over `WorkstreamArtefacts` (pure), `EvidenceBlockReader` for
   the last `theseus-evidence` block, `SpecAcceptanceStore` keyed by project, slug and the accepted spec's commit.
   *Tests first:* a test per stage artefact, per gate state, the ahead-of-gate case, and work from before v0.2.
5. **Membership and placement** (AC 4, 10): `Workstream.Project` and `Workstream.Slug`, kept in the board file;
   `ProjectMembership` (slug clash: two cards share a slug only when one is a verifier); `ProjectLayout` (cards at
   their stage per objective, No slug yet, Not in the project file, not started, earlier slugs, the verifier in
   verify). `propose_workstream` gains `slug`; an approved proposal from a project card inherits its project; a
   verifier card takes its subject's project and slug.
   *Tests first:* persistence across a refresh and another write (P21), the layout cases, the MCP tool's schema and
   refusal of a malformed slug.
6. **App** (AC 1, 4): Settings gains **Project repositories** (one folder per line); the card menu and the menu bar's
   Workstream menu gain **Project** (project, then slug, or clear), shown only while a project is readable.
   *Tests first:* the setting saves and reads back; the board with no repositories set offers no item (AC 22).

## Risks

- `git` runs as a child process on refresh. With no repositories set nothing runs (AC 22). A slow repository logs
  itself through the refresh's existing step timing.
- Real-git tests depend on git on the path. CI's `windows-latest` has it.

## Pull request 2 (the project card and the view)

For `spec.md` ACs 12 to 18 and pull request 1's remainders (AC 4's groups shown, AC 5's tooltip, AC 7's Accept spec,
AC 10's "ended").

1. **Stages read in the background** (Core). The background read also reads every slug's artefacts
   (`ProjectArtefacts.Attach`, which runs `WorkstreamArtefactReader` per row), so nothing on the UI thread runs git;
   the UI thread only combines them with the acceptance store through `StageCalculator`. `VerifierEnding.HasEnded`:
   a verifier has ended when its report back has been accepted. *Tests:* a real repository, and the rule.
2. **The board** (App). `BoardViewModel.Cards` becomes every card loaded; a card of a readable project is kept out of
   the grid (AC 13); each project card sits in a cell, its column and row remembered in settings and changed by
   dragging (AC 12); Shift-selection follows the project view's order while it is open (AC 15). The rows-by-folder
   mode's label becomes Folder (AC 17). *Tests:* the board fixture: with no projects the grid is as before (AC 22);
   a readable project's cards leave it and an unreadable one's come back (AC 13); the project card's place survives.
3. **The project card** (`ProjectCardViewModel`): name, current phase, n of m objectives, the waiting count and its
   list (AC 12). *Tests:* view-model tests for each kind of waiting item.
4. **The project view** (`ProjectViewModel` and its rows, cells, entries and tree nodes; `ProjectView.xaml` as its
   own user control). The tree; a row per objective with workstreams across the seven columns; the No slug yet and
   Not in the project file groups; not started and earlier slugs; the verifier in verify; gate marks; Waiting on you
   with Open spec and Accept spec (a confirmation naming the slug and commit; it writes the store); the source in the
   card's tooltip. Opened from the project card (double-click, its menu) and View › Open project; Board › breadcrumb
   back. Every command also on the menu bar. *Tests:* view-model tests, a layout test of the user control, and markup
   tests of the wiring.

## Pull request 3 (the terminal window)

For `spec.md` ACs 19 to 21, and AC 22 for them.

1. **The window name** (AC 19). `WindowGroupName.For` gains the readable projects: after a card's own window
   override and before its area and column, a card in a readable project gets
   `<terminal window name>-project-<project>`. An unreadable or unlisted project's card is named as before, as it sits
   on the grid as before (AC 13). *Tests first:* the name, its place in the order, and that with no readable project
   every card gets exactly the name it got before, project or not (AC 22).
2. **Chosen at each start, recorded on the run** (AC 20). `LaunchCoordinator` asks the shell for the readable projects
   at each start; `SessionRun.WindowName` records the window each run was launched into, and run log lines from before
   it read as no window. *Tests first:* the run log round-trips the window and still reads an old line.
3. **Bring the window forward** (AC 21). `ProjectWindow` picks a live session of the project's cards whose latest run
   was launched into the project's window, or says why there is none. Shown in the project view beside the
   breadcrumb and on the menu bar (View › Bring project window forward), disabled with the reason in its tooltip.
   *Tests first:* the choice and each reason; the markup of both places.
4. **Holdout.** "Resume tomorrow" applies; verify stays open until a verifier card has run it (P18). Not run here.
