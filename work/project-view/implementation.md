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
