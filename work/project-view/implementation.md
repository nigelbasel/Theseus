# Implementation: objective 1.1, pull request 1

Pipeline `feature`, stage **implement**, from `plan.md`, for `spec.md` ACs 1 to 11 and the `method-v0.2` release. Branch
`nigel/project-view`, repo Theseus. Pull requests 2 (the project card and view) and 3 (the terminal window) are not
started.

## What was built, per AC

| AC | State | Where |
|---|---|---|
| method-v0.2 | done | `method/project.md` (the shape and what is read, the owner's gates in a project, the evidence result block, promotion), `method/pipelines/feature.yaml` and `bug.yaml` (plan and review are evidence gates, `<issue>` becomes `<slug>`, `version: 1`), `method/README.md` (version, what v0.2 adds, two decisions), `projects/theseus-bootstrap.md` (rewritten to the shape, `Kit: method-v0.2`, `Primary branch: nigel/theseus`; a script checked that every criterion line, text and tick, is unchanged). P18 was already in the README's rules |
| 1 | done | `TheseusSettings.ProjectRepositories`, empty by default; Settings' **Project repositories** field, one folder per line (`SettingsWindow.xaml`, `SettingsViewModel`) |
| 2 | done | `Projects/GitCli`, `GitRepositoryReader`, `ProjectCatalog`: read with git (`ls-tree`, `cat-file`), from the primary found on the default branch's copy; not a repository, a missing folder, git missing and a primary branch not on the machine are each unreadable with the reason. Nothing fetches: a test records every git command sent |
| 3 | done | `Projects/ProjectFileReader` and the format records; `ProjectFileReaderTests`: a test per shape, 25 refusals each with its line and reason, `method-v0.1` pinned, and the repository's own project file read |
| 4 | partly | Done: `Workstream.Project` and `Slug`, kept in the board file (`WorkstreamSidecar`, `WorkstreamAssembler`, `BoardViewModel.Persist`); `propose_workstream`'s optional `slug`, checked for shape; an approved proposal inherits its proposer's project; a verifier takes its subject's project and slug; **Project** on the card's menu and the Workstream menu, shown only while a project is readable; the one-slug rule (`ProjectMembership`); the view's grouping, No slug yet, Not in the project file, not started and earlier slugs, computed in `ProjectLayout`. Not done here: showing those groups, which is the view (pull request 2) |
| 5 | partly | Done: `WorkstreamArtefactReader` reads the workstream's branch while unmerged, the primary once merged, not on this machine when neither has it; `WorkstreamSource.Describe` gives the branch and commit. Not done here: the card tooltip that shows it (pull request 2) |
| 6 | done | `PipelineStage`, `StageCalculator`: the seven columns, a bug's stages under them from `reproduction.md` and `fix.md`'s `## Spec`, `## Cause` and `## Change` headings, no plan for bugs |
| 7 | partly | Done: the spec gate passes only on an acceptance in `SpecAcceptanceStore` (Theseus's data folder, keyed by project, slug and the accepted spec's commit) matching the spec as it now reads, else **changed since accepted**; verify passes only on a `pass` in the last `theseus-evidence` block, else **no evidence** or the result; the block's `gates` are kept as information and pass nothing; the primary's pull request gate is **not read yet**; the pipelines say plan and review are evidence gates. Not done here: Accept spec on the board, which writes the store (AC 15, pull request 2) |
| 8 | done | `StageCalculator`: a card sits at its furthest reached stage, capped at spec while the spec gate has not passed, with **ahead of your gate: plan written** (the furthest artefact's name) |
| 9 | done | `WorkstreamArtefactReader.KitMoveParent` and `IsBeforeKit`; `StageCalculator` shows the reached stage with **before method-v0.2** and no gates |
| 10 | partly | Done: a verifier card takes its subject's project and slug; it shares the slug; `ProjectLayout` puts it beside its subject, in verify whatever the artefacts say, until a `hasEnded` test says it has ended. Not done here: what "ended" is read from (the view's, pull request 2) |
| 11 | done | `ProjectObjective.IsDone`, `ProjectFile.ObjectivesDone`, `ProjectFile.CurrentPhase` |

The user help gained a short **Projects** section in `docs/cards.md`, for the new menu item and setting.

## Decisions

Each is the most reversible reading of the spec I could find; the owner may want to look at all of them.

1. **Finding the primary when the default branch is still at method-v0.1.** The spec finds it on the default branch.
   Until the primary is merged into `main`, `main`'s copy is the v0.1 file, which names no primary, so the project
   could never be read. The reader therefore also looks at the branch the folder has checked out, after the default
   branch, and uses the first copy at v0.2 that names a primary. Once `main` carries a v0.2 file, the default branch
   decides, as the spec says. (Changed by review fix 4: a primary once found is remembered and read first.)
2. **Default branch:** the one origin's HEAD names (the local branch of that name if there is one, else origin's copy),
   else a local `main`, else `master`. The primary and workstream branches are local branches only.
3. **A spec acceptance is compared by content, not commit.** The store keeps the commit the spec was accepted at (as
   the spec asks) and a hash of its text; the gate compares the hash, so merging the workstream into the primary does
   not undo an acceptance and any edit to the text does. A bug's rule is identified by its `## Spec` section alone, so
   writing the fix's `## Cause` does not make the rule "changed since accepted".
4. **Not on this machine** means the workstream's branch is not a local branch and the primary has no `work/<slug>/`
   either. A branch deleted after its merge is read from the primary.
5. **Before method-v0.2** means: read from the primary (merged), and the last commit that touched `work/<slug>/` is an
   ancestor of the commit before the oldest one that added `**Kit:** method-v0.2` to the project file. Work written to
   again after the move carries gate markers.
6. **A stage passes when any later stage is reached**, not only the next one, because a bug's rule is optional; a stage
   not reached with a later one reached is **skipped**. Compound passes when reached. A bug fixed without a rule has
   its spec gate skipped and is not held at spec.
7. **A feature with later artefacts and no spec.md** sits at spec, held by the gate, with the ahead note.
8. **Verify:** an `evidence.md` with no block, or a block that is not JSON or has no result, is **no evidence** (with
   the reason); a block whose result is `fail` or `open` is waiting, noted with its result.
9. **Two extensions to AC 3's shape**, both in `method/project.md`: `**Pipeline:** feature or bug` for objective 0.3,
   whose six changes ran both pipelines (each slug's is then read from its artefacts: `reproduction.md` or `fix.md`
   means bug); and `**Pipeline:** [to be chosen]` for an objective no workstream serves yet, so no pipeline was
   invented for objectives 1.2 to 5.1. Also: phases are numbered from 0 one after another; several exit gate paragraphs
   under one phase are joined (phase 1 has two); any table under an objective must be the workstreams table; a criterion
   in a phase outside an objective is refused; an objective with no criteria yet is not done.
10. **The project file rewrite.** Objectives 0.1 to 0.3 list "the Theseus workstream" on every row, so their slugs are
    earlier slugs of one workstream (AC 4). 0.1 and 0.2 had no artefacts folder; they got slugs `method-kit` and
    `holdout-scenarios`, and 0.2's branch is `[not recorded]`. 0.3's change descriptions and per-change pipelines are
    kept as a prose list under its table. 1.0's bullet became the prose under its existing heading, starting "First.".
    Phase 2 to 4 objectives became headings, their titles' first letters capitalised, with "[criteria to be written]".
    Only workstreams the file already named were added (`verifier-card` under 1.0, `project-view` under 1.1); the other
    `work/` folders and branches (close-step, handoff-names, links-tree-scrollbar, prerelease-channel) are not assigned
    to an objective, because the file does not say which they serve. close-step stays in 0.3's prose as before.
11. **The menu item is "Project", a submenu**, not "Project…": the ellipsis means a dialog in a Windows menu (R16). It
    lists each readable project (its slugs, and No slug yet), then Not in a project. On the menu bar it is under
    Workstream and acts on the ticked cards; on a card it acts on the selection when the card is in it.
12. **Unreadable projects in this pull request** are listed disabled in the Project menu, with the reason in the
    tooltip, when at least one other project is readable, and logged once whenever the readings change. Showing them on
    the project card is pull request 2.
13. **Reading cost.** A read is several git processes; the board reads the projects again on a refresh only when the
    folders changed or 30 seconds have passed. (Review fix 2 moved the read off the UI thread.)
14. **A proposed slug** is checked only for its shape. If another card holds it when the proposal is approved, the new
    card joins the project with no slug yet (logged, and since review fix 11 said on the board), rather than the
    approval failing.
15. **Project identity** is the project file's name without `.md`; two listed repositories with a project file of the
    same name are not told apart.
16. **A slug with no card whose work exists** (an earlier slug, or stage 0 work with no card on this machine) is shown at
    its own stage; only a slug with no card and nothing written is "not started".
17. **The `method-v0.2` tag** is not created: tags are refs, and the version is released when the owner merges.

## Differences from the plan

- The plan named a pure `StageCalculator` over `WorkstreamArtefacts`; it also gained `PipelineFor` (decision 9) and
  `ArtefactName` (the ahead note's wording).
- `ProjectMenu` and `ProjectChoice` in the App were not named in the plan; they hold the menu's logic so it can be
  tested without a window.
- `docs/cards.md` was not in the plan.
- **One existing test changed:** `SettingsStoreTests.Write_RoundTripsEveryField` sets every list-valued setting and
  compares the lists one by one, because a record compares lists by reference; it then compares the rest with the
  lists copied across. Without the new `ProjectRepositories` in that copy it failed on reference equality alone, not
  on a wrong value. It now sets the new list, asserts it round-trips, and copies it like the others. Nothing it asserted
  before is weakened.
- New tests were added to `McpProposalToolsTests.cs` beside the proposal tool's other tests; none of them is a
  reproduction test.

## Not checked

- Nothing was checked against a rendered board: the Settings field and the Project menu are covered by view-model
  tests only. AC 22's "no new menu item" rests on `ProjectMenuTests` (no readable project, no items) and the binding of
  the items' visibility to that.

## Suite

`dotnet test Theseus.slnx -p:NuGetAudit=false` on `nigel/project-view` at the membership commit: **1907 of 1907 passed,
0 warnings** (the workstream log). The base was 1762; 145 tests are new. `ProjectMenuMarkupTests` checks the menu
markup on both menus; the rendered board was not checked.

## Review fixes

From the review of pull request 1. Each has its test first, in a new file named `*ReviewFixTests` where the area had
one, else beside the area's tests.

| Item | Fix | Test |
|---|---|---|
| 1 (must) | `GitRepositoryReader.Names` lists files only (`ls-tree` entries of type `blob`), so a folder named `notes.md` under `projects/` is not taken for a project file. A file no branch can give is **unreadable: … could not be read on any branch** instead of an index out of range. Each folder's read is wrapped: anything thrown is logged and that folder shows unreadable with the message; `BackgroundProjectReads` wraps the whole read the same way, so nothing from project reading can reach `Refresh` | `ProjectCatalogReviewFixTests`, `BackgroundProjectReadsTests.AReadThatThrows_…` |
| 2 | `BackgroundProjectReads` (Core) runs the read on a worker thread and posts the result back through the dispatcher; one read at a time, other folders read straight after the one under way, the same folders again only after 30 seconds, nothing run with no folders. The shell asks for a read on each refresh and rebuilds the menus when the result lands | `BackgroundProjectReadsTests` (a reader that blocks until released) |
| 3 | The move to method-v0.2 is found with `log --first-parent --diff-merges=first-parent -S…` on the primary, so a move merged in is the merge commit and its first parent is the primary as it stood | `WorkstreamArtefactReaderReviewFixTests.WorkMergedWhileTheKitMoveWasOnItsOwnBranch_…` |
| 4 | `ProjectPrimaryStore` (`project-primaries.json` in Theseus's data folder) remembers each project's primary per listed folder. The order is now: the remembered primary while that branch is still here, then the default branch's copy, then the checked-out branch's. The old test `TheBranchTheFolderHasCheckedOut_DoesNotMatter` is renamed `AFileGoneFromTheCheckedOutBranch_IsStillReadFromThePrimaryTheDefaultBranchNames`, which is what it proves | `ProjectCatalogReviewFixTests.OnceFound_ThePrimaryIsRemembered_…`, `ARememberedPrimaryThatIsGone_IsFoundAgain` |
| 5 | A pipeline is exactly `feature`, `bug` or `freeform` (any case), joined by ` or `, or `[to be chosen]`; `feature, bug` and numbers are refused | `ProjectFileReaderReviewFixTests` |
| 6 | An indented tick box is a criterion of its own (the reversible choice: it is counted, so an unticked one keeps the objective open), not a continuation of the one above | `ProjectFileReaderReviewFixTests.AnIndentedTickBox_…` |
| 7 | `ProjectChoice` compares by what it shows, ignoring its command; the shell replaces a card's or the menu bar's choices only when they differ, so an open submenu stays open across a refresh | `ProjectMenuTests.TheSameMenuBuiltTwice_IsEqual_…` |
| 8 | `GitRepositoryReader.Ref` turns every branch name into `refs/heads/<name>` (commit ids and full refs are left alone; a default branch found only as origin's copy is returned as its full ref), so a tag of the same name cannot win and a table cell starting with `-` never reaches git as an option | `ProjectCatalogReviewFixTests.ATagNamedLikeThePrimary_DoesNotWin`, `WorkstreamArtefactReaderReviewFixTests.ABranchCellThatLooksLikeAnOption_…` |
| 9 | A folder git refuses is reported in git's own words (its standard error), so "detected dubious ownership" reads as such | `ProjectCatalogTests.AFolderThatIsNotARepository_IsUnreadable` |
| 10 | `GitCli` waits for git's output pipes only for what is left of its timeout after git exits, and reports that it did not close | (no test: it needs a child that outlives git) |
| 11 | A dropped proposed slug is also said on the board, after Created, or after the start when the card starts at once | (the shell's create path has no test fixture; the logic it uses is `ProjectMembership`'s, tested) |

From the re-review:

| Item | Fix | Test |
|---|---|---|
| R1 | A remembered primary is checked against its own file's `Primary branch:` line: when that names another branch that is here, it is followed and remembered; when that branch is not here, the primary is found again from the default branch | `ProjectCatalogReviewFixTests.APrimaryWhoseFileNamesANewPrimary_…`, `ANewPrimaryNotOnThisMachine_…` |
| R2 | Folders changed and changed back during a read no longer leave the in-between folders queued: a request for the folders being read clears what was queued | `BackgroundProjectReadsTests.FoldersChangedAndChangedBack_…` |
| R3 | The dropped-slug notice after a start is added to the notice already showing, joined with a space as other notices are, rather than replacing it | (the shell's create path has no test fixture) |

**An existing test's expectation changed (item 9):** `ProjectCatalogTests.AFolderThatIsNotARepository_IsUnreadable`
asserted the reason was exactly "not a git repository", Theseus's own words. The review asked for git's words, so it now
asserts the reason starts with git's `fatal: not a git repository`. That is a correction of what the owner is told, not
a weakening: the folder is still unreadable and the reason still says why.

Suite after the review fixes, at their commit: **1925 of 1925 passed, 0 warnings** (the workstream log); 18 tests are
new.

A branch name that is all hex digits (seven or more) is taken for a commit id by `Ref`; no branch in this repository is
named so.

## Open for the owner

- The slugs `method-kit` (objective 0.1) and `holdout-scenarios` (0.2) were invented, since neither had a `work/`
  folder, and 0.2's branch is `[not recorded]`. Rename or confirm them.
- AC 9's "before method-v0.2" is measured from the commit that added `**Kit:** method-v0.2`. Once a v0.3 exists and the
  project moves to it, the label will need measuring from that next move (and the reader will need to read v0.3).
- The workstreams not yet placed under an objective: close-step, handoff-names, links-tree-scrollbar, prerelease-channel.

# Pull request 2

Pipeline `feature`, stage **implement**, from `plan.md` ("Pull request 2"), for `spec.md` ACs 12 to 18 and pull
request 1's remainders.

## What was built, per AC

| AC | State | Where |
|---|---|---|
| 12 | done | `ProjectCardViewModel` and `ProjectCardTemplate` in a cell's project strip (`ColumnViewModel.Projects`): name, current phase, n of m objectives done, the waiting count with its list on hover (`ProjectWaiting`: sessions waiting for input, reports waiting for confirmation, proposals pending, specs waiting). Dragged like a card; its cell is kept in settings (`ProjectCardPlaces`) |
| 13 | done | `BoardViewModel.KeepOutOfGrid`, from `ProjectGridRule`: a readable project's cards leave the grid and stay in `Board.Cards`, so Start, Stop and selection reach them; an unreadable, pinned or unlisted project's come back. The session drawer and Sessions panel are untouched. An open view whose project stops reading closes with a notice |
| 14 | done | `Views/ProjectView.xaml`, its own user control, in place of the board in the main grid (`IsBoardShown`, `IsProjectOpen`); opened by double-click, Open on the project card's menu, or View › Open project; breadcrumb **Board › <project>** back. The drawer, side panels and status bar are outside it and stay |
| 15 | done | Tree of phases (current marked), objectives (ticked n of m) and slugs (stage, gates passed, Waiting on you); selecting scrolls to the row. A row per objective with workstreams across the seven columns; cards use the board's `CardTemplate`, so face, menu and click selection are the board's, and Shift follows the view's order (`Board.SelectionOrder`). Cards are not dragged in the view. Waiting on you with Open spec and Accept spec. Every command is on the menu bar: View › Open project, View › Back to the board, Workstream › Open spec, Workstream › Accept spec, and the existing Start selected and the rest |
| 16 | done | The view has no fold; folding on the board is unchanged |
| 17 | done | View › Rows › **Folder** (was Project); the setting is still saved as `Project`; `docs/board.md` says so |
| 18 | done | A promoted ad hoc card joins through PR 1's Project menu, keeps its key, and from then on is in the view and out of the grid (it is the same rule as AC 13) |
| 4 (rest) | done | No slug yet and Not in the project file are rows of the view; not started and earlier slugs show at their own stage, not as cards |
| 5 (rest) | done | `WorkstreamCardViewModel.FaceTooltip`: the working directory, then for a project card the branch and commit read; an ad hoc card's tooltip is unchanged |
| 7 (rest) | done | Accept spec asks with `SpecAtGate.ConfirmationQuestion` (slug, project, short commit, source) and only a yes writes `SpecAcceptanceStore` (`AcceptIfConfirmed`) |
| 10 (rest) | done | `VerifierEnding.HasEnded`: a verifier has ended once its report back is accepted, the moment it is also stopped; it then leaves its subject's row |
| 22 | done | With no project repositories nothing runs, no rule keeps a card out of the grid, no cell holds a project card, the menu items are hidden, and tooltips are unchanged; the strip with no project card lays out at no height (`ProjectViewMarkupTests`) |

The stages' git reads run inside the background read (`ProjectArtefacts.Attach`, per slug, after the catalogue); the
UI thread only combines them with the acceptance store.

## Decisions

1. **`Board.Cards` is every card loaded**, not the cards in cells. Before this the two were the same set; now a
   project's cards are kept out of the cells and must still be started, stopped and refreshed. (Review fix 1: the
   selection commands act on what is shown, `Board.ShownCards`, not on `Board.Cards`.)
2. **The project card's place** is remembered by project name in settings. It is dropped on a cell's cards or its
   project strip. A column or row the current view does not have puts it in the first cell; dragging follows the
   board's rule (only in the stages view).
3. **The waiting count's reports** are those waiting for confirmation (state Reported) on a link that touches one of
   the project's cards; lost reports are not counted.
4. **Waiting on you sits on the waiting entry in the spec column**, with its two buttons, rather than on a line drawn
   between columns: the view draws no lines (departure 4).
5. **Open spec opens a copy** of the spec as it was read, at the commit Accept names, in Theseus's data folder
   (`specs/<project>-<slug>-<commit>.md`): the folder's checkout may hold another version.
6. **Placement of slugs with no stage**: a slug with nothing written, or not on this machine, shows in the first column
   with its note; a freeform slug, or one whose objective has no pipeline chosen, shows only in the tree unless a card
   holds it.
7. **The view is rebuilt only when what it shows changes** (`Signature`), so the tree's selection and the scroll stay
   put across refreshes.
8. **The rubber band selects on the board only**; in the view, selection is by click, Ctrl+click and Shift+click.
9. **The menu bar's project items are hidden** while there is nothing to offer (no readable project, no open project,
   no waiting spec), which keeps a board with no projects exactly as it was.
10. **Read cost.** Each background read now also reads every slug's artefacts, several git commands a slug, every 30
    seconds while repositories are listed; none of it on the UI thread.

## Not checked

- The whole window was not rendered: the user control is laid out in a test (tree, columns, rows, Waiting on you), the
  window's wiring is checked in its markup, and the empty project strip is laid out alone.
- Dragging a project card, the double-click, Open spec's launching of an editor and the message box are not covered by
  tests: they are code-behind and App wiring around tested view models.

## Suite

`dotnet test Theseus.slnx -p:NuGetAudit=false` at the pull request 2 commit: **1964 of 1964 passed, 0 warnings** (the
workstream log); 36 tests are new since pull request 1's 1928. One existing test changed: the settings round-trip
copies the new `ProjectCardPlaces` dictionary across, as it does every other collection, and asserts it round-trips.

## Pull request 2 review fixes

| Item | Fix | Test |
|---|---|---|
| 1 (must) | Select all, Invert, Select live, their commands' enabling and the "n selected" count act on `Board.ShownCards`: the grid's cards on the board (folded rows and columns included, as Select all has always ticked them), the view's cards while it is open. Clear selection still clears every card. Opening and closing the view untick the cards that leave sight (`Board.DeselectUnshown`) | `ShownSelectionTests` (each command on the board and in the view, the switch both ways, no projects, a folded row) |
| 2 | The background read lands in two posts: the catalogue (membership, so which cards leave the grid), then the slugs' artefacts. Before the first catalogue nothing reads as readable, so no card is held out of the grid. A fresh reading shows the artefacts it had until the new ones land | `BackgroundProjectReadsTwoStepTests`, `ReadingContinuityTests.AFreshReading_…` |
| 3 | The stale comment on Invert ("a query over the rows") is gone with the code; Select live's ("nothing is held back") and Select all's are rewritten; `Board.Cards`'s now says the selection acts on `ShownCards` | (comments) |
| 4 | Where a slug was read is out of the view's signature and the entries' details; `ProjectEntryViewModel.Source` is live, and an unchanged view takes the new sources in place (`TakeSourcesFrom`) | `ProjectViewReviewFixTests.ANewCommit_…` (two) |
| 5 | The acceptances are loaded once per wiring; the board reads the project cards' places from settings once and keeps them, updated by a drag, and not at all with no projects | (behaviour unchanged; covered by `BoardProjectTests`, `ProjectViewModelTests`) |
| 6 | A verifier stays ended once its report was accepted, even if the link is removed afterwards; the shell passes every link, removed ones included | `ProjectArtefactsTests.AVerifier_HasEnded_…` |
| 7 | `MoveProject` refuses where cards cannot be dropped (the session-state view), and the project strip's drop handler checks `CanDrag` too | `ProjectViewReviewFixTests.AProjectCard_IsNotMoved_…` |
| 8 | Workstream › **Ope_n spec** (n is free beside St_op, Acc_ept and the rest) | `ProjectViewMarkupTests` |
| 9 | `ReadingContinuity`: a project that read and then fails keeps its last good reading, with the failure in the project card's tooltip; a second failure in a row makes it unreadable; a folder no longer listed drops at once | `ReadingContinuityTests`, `ProjectViewReviewFixTests.AProjectCard_SaysWhenItsLastReadFailed` |

Suite after these fixes: **1982 of 1982 passed, 0 warnings** (the workstream log); 18 tests are new.

**An expectation of this pull request's own test changed (item 6):** `ProjectArtefactsTests.AVerifier_HasEnded_…`
asserted that removing an accepted report link brought the verifier back; the review judged that wrong, and it now
asserts the verifier stays ended.

**Decision (item 9):** one failed read no longer closes the view or brings a project's cards back to the grid; the
second in a row does. This softens AC 13 ("if the project becomes unreadable … its cards return") by one read, about
30 seconds, in exchange for a transient git failure not reshuffling the board. Removing a repository from Settings still
returns its cards at once.

**Decision (item 1):** on the board "shown" is the grid's cards including folded rows and columns, not
`Board.VisibleCards` (which leaves folded ones out), so Select all with no projects ticks exactly what it always did
(AC 22).

From the re-review of these fixes:

| Item | Fix | Test |
|---|---|---|
| R1 | A ticked card that leaves the grid (its project's first catalogue lands, or it joins a readable project) is unticked at the end of every placement, and the selected-card commands take `Board.ChosenCards`, the ticked cards among those shown, so Stop or Archive selected never reaches a hidden card | `ReReviewFixTests` (the catalogue landing, joining, a hidden card ticked anyway) |
| R2 | `BranchTips` reads `%(refname)` and strips `refs/heads/`: with a tag of the same name, `%(refname:short)` printed `heads/<name>` and the cache lost the primary or took a workstream branch for merged | `ArtefactCacheReReviewTests.TagsNamedLikeThePrimaryAndTheWorkstreamBranch_…` |
| R3 | The cache keeps only the latest entry per branch for the folder listings and the merged check, and the latest primary tip for the move to v0.2; a rewound branch costs one fresh read | `ArtefactCacheReReviewTests.TheCache_KeepsOneEntryPerBranch_…` |
| R4 | A slug's files are read at the commit its source names, not by branch name, so its files, its tree and the commit shown always agree; the move to v0.2 is looked up at the primary's snapshotted tip | `ArtefactCacheReReviewTests.FilesAreRead_AtTheCommitTheSourceNames_…` |
| R5 | Until a project's slugs are first read they say **reading…** (in the pipeline and the tree), no spec counts as waiting, and the project card's badge shows the count with an ellipsis and its tooltip says it is still reading | `ReReviewFixTests.BeforeTheFirstArtefactsLand_…`, `AProjectCardStillReading_…` |

Suite after the re-review fixes: **1995 of 1995 passed, 0 warnings** (the workstream log); 8 tests are new.

### The background read's cost (was open; closed)

With about 15 slugs the artefact step was roughly 9 seconds of git every 30 seconds. `ArtefactCache` now re-reads a
slug's files only when what they are read from has moved:

- each read starts with every branch tip in one command (`for-each-ref refs/heads`); with no tip moved that is the
  only command run;
- whether a workstream branch is merged is worked out again (`merge-base --is-ancestor`) only when its tip or the
  primary's moves, so a merge moves the slug to the primary as before;
- each source's `work/` subtree ids are read in one `ls-tree` per branch tip, and a slug's files are read again only
  when its subtree on the source actually used, the source itself, its merged state, or (for a merged slug) the move to
  method-v0.2 differs from the last read. Otherwise the last artefacts are kept with the source's new tip, so the
  tooltip still names the current commit.

The spec's identity and "before method-v0.2" come from those artefacts, so they are right whenever only the primary
moves: an unmerged slug keeps its branch's files; a merged one is re-read when its subtree changes; the move to
method-v0.2 is looked up again for each primary tip.

| Test | What it shows |
|---|---|
| `ArtefactCacheTests.WithNoTipMoved_…` | a second read with nothing moved runs one git command (`for-each-ref`) |
| `ArtefactCacheTests.AMovedBranchTip_…` | a moved branch re-reads its slug and no other |
| `ArtefactCacheTests.AMerge_MovesTheSlugToThePrimary_…` | a merge moves the source to the primary, merged, at its tip, with the spec's identity unchanged |
| `ArtefactCacheTests.OnlyThePrimaryMoving_…` | the primary moving re-checks the merge but reads none of an unmerged slug's files |
| `ArtefactCacheTests.TheCachedArtefacts_…` | the cache gives what the uncached reader gives |

Suite after the cache: **1987 of 1987 passed, 0 warnings** (the workstream log).

Known edge: a commit that changes a slug's folder and then restores its exact tree is not re-read, so a "last commit"
within it can lag until the folder next changes. The cache keeps entries per tip for as long as the board runs; they
are small.

# Pull request 3

Pipeline `feature`, stage **implement**, from `plan.md` ("Pull request 3"), for `spec.md` ACs 19 to 21 and AC 22 for
them.

## What was built, per AC

| AC | State | Where |
|---|---|---|
| 19 | done | `WindowGroupName.For` takes the readable projects: after a card's own window override and before its area (and the area's kept name) and column, a card in a readable project opens in `<Settings' terminal window name>-project-<project file name>` (`WindowGroupName.ForProject`). The `-project-` keeps it apart from an area of the same name |
| 20 | done | `LaunchCoordinator.ReadableProjects` is asked at each start, so a card that joined a project mid-session opens in its window at its next start, and resuming tomorrow opens it there. `SessionRun.WindowName` records the window each run was launched into; run log lines from before it load with no window |
| 21 | done | `ProjectWindowTarget`: a live session of the project's cards whose latest run was launched into the project's window, else the reason. **Bring window forward** beside the project view's breadcrumb and View › **Bring project window forward** use it, through Focus's own `TerminalFocus.FocusSession`; disabled with the reason in the tooltip, shown on the disabled control |
| 22 | done | With no project readable, every card's window name is exactly what it was, project field or not, across lanes, areas and kept area names (`ProjectWindowNameTests.WithNoProjectReadable_…`, 12 cases); the menu item shows only while a project is open |

## Decisions

1. **The project window is for readable projects only.** A card whose project is unreadable, pinned or unlisted opens
   where it did before, as it sits on the grid as before (AC 13). A verifier card, which carries its subject's project,
   opens in the project's window too.
2. **"Launched into the project's window" is read from the session's latest run.** A session resumed into another
   window (say, before its card joined the project) does not count, and neither does a run from before windows were
   recorded.
3. **Bring window forward focuses that session's tab**, as Focus does; landing on the window but not the tab is
   reported as Focus reports it.

## Not checked

- `LaunchCoordinator` has no test harness (it starts processes and a pipe server); the window it passes to the
  launch is `WindowGroupName.For`'s, which is tested, and the run it records carries that name (code read, not tested).
- Raising a real Windows Terminal window was not tried.
- **The holdout scenario "resume tomorrow" applies to this pull request and has not been run**: this session cannot
  reach the holdouts. Verify stays open until a verifier card has run it (P18).

## Suite

`dotnet test Theseus.slnx -p:NuGetAudit=false` at the pull request 3 commit: **2022 of 2022 passed, 0 warnings** (the
workstream log); 27 tests are new since 1995. One test of pull request 2 changed: the view's layout test found the
breadcrumb as the first button, and the new Bring window forward button now comes first in the tree, so it finds the
breadcrumb by its name instead; what it asserts is unchanged.

## Pull request 3 review fixes

| Item | Fix | Test |
|---|---|---|
| 1 | Bring window forward takes each live card's latest run by its workstream key, not by its session id: a resume that dies is reminted under a new session id, which the card follows and its run does not (this is what decision 2 meant) | `ProjectWindowTargetTests.AResumeRemintedUnderANewSession_…`; the other cases now give each card its own key |
| 2 | Until this run's first catalogue lands, the readable projects are the ones remembered from the last catalogue (`readable-projects.json` in Theseus's data folder, `RememberedProjects`), so the morning's first starts open in the project's window; nothing waits for the read. Once a catalogue lands, only what reads now counts, and that is remembered for next time. Only the names are kept: which cards belong to which project is already in the board file | `RememberedProjectsTests` (before the catalogue; a project gone unreadable since; no folders listed; an unreadable file) |
| 3 | A refresh reads the run log once: the lifecycles step's reconciled runs are kept and shared with the busy-card check and Bring window forward; a busy check outside a refresh (renaming a handoff) still reads it fresh | (the shell has no test harness; behaviour unchanged) |
| 4 | An area whose window would equal a readable project's window (an area named "Project P") gets `-area` added, so the two never share a window; with that project not reading, the area is named as before (AC 22). The reversible choice: nothing is refused and no setting changes | `ProjectWindowClashTests` |

Suite after these fixes: **2029 of 2029 passed, 0 warnings** (the workstream log); 7 tests are new.
