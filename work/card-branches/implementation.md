# card-branches: implementation

Pipeline: `feature` (method kit v0), stage **implement**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Follows `plan.md` (approved by Nigel, 2026-11-18).

## What was done

| File | Change |
|---|---|
| `src/Theseus.Core/Domain/GitRepository.cs` | New: `RootOf(path)` and `IsRoot(directory)`, from the file system only |
| `src/Theseus.Core/Domain/CardRepo.cs` | New record: path, folder name, branch |
| `src/Theseus.Core/Domain/CardRepos.cs` | New: `Find` (AC 1, 2) and `Read` (with branches via `GitBranchReader.Read`) |
| `src/Theseus.Core/Domain/BranchLine.cs`, `BranchLines.cs` | New: the line's text, tooltip and "differs" flag (AC 3 to 9) |
| `src/Theseus.Core/Domain/PullRequestLink.cs` | Optional `Note` |
| `src/Theseus.Core/Domain/PullRequestResolver.cs` | Declared repos with one origin link to it; with several, no link and a note (AC 11); none, today's path |
| `src/Theseus.App/ViewModels/WorkstreamCardViewModel.cs` | `Branch` now holds the worded line; new `BranchTooltip`, `BranchesDiffer` |
| `src/Theseus.App/ViewModels/ShellViewModel.cs` | Sets the three from `BranchLines.For(...)` |
| `src/Theseus.App/ViewModels/CardItemRows.cs` | A PR row with no title falls back to the link's note |
| `src/Theseus.App/MainWindow.xaml` | The branch line binds to the worded text and tooltip, amber when branches differ |
| `docs/cards.md` | The branch line row (AC 13) |
| `tests/Theseus.Tests/Domain/CardReposTests.cs`, `BranchLinesTests.cs`, `PullRequestResolverRepoTests.cs` | New, 17 tests, failing to build before the code |

Full suite: **1508 of 1508**, 0 warnings. No existing test file changed.

## Every difference from the plan

- **The PR note is shown as the row's title, not as a tooltip fallback in XAML.** PR rows already show a title; when a
  session has reported none, the note ("in one of api, ui") takes its place. No XAML change for PRs was needed.
- **The amber is the literal `#D68910`, not a theme brush.** It is the colour the board already uses for Paused and
  Blocked (`docs/cards.md`: "in amber"); `SystemFillColorCautionBrush` is not used anywhere in this theme and would
  have failed silently if missing.
- **A repository whose branch cannot be read is shown as "branch not readable"** and, among several, counts as
  differing. The spec did not cover it; the alternative was hiding the repository.
- **Corrected before commit:** a single unreadable repository was first flagged as "differs"; the spec marks a mismatch
  only among several, so it is now not flagged.
- `GitBranchReader` was not changed; `GitRepository` repeats its upward walk in a simpler form (it needs the root, not
  the branch).

## For the verifier and reviewer

- AC 3 ("exactly as today") is tested for a repo and a non-repo working directory; the XAML binding change is not, and
  the live check covers it.
- Every card now reads `addDirs` on each refresh. Cards that declare none (84 of 87 on the owner's board) do no extra
  work beyond an empty enumeration.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| implement | 2026-11-18 21:06:36 (logged) | this commit's time | 1508/1508 |

## Pass 2 (after verify part 1 failed, `evidence-part1-attempt1.md`; option A, Nigel)

| Finding | Done |
|---|---|
| F1 (defect) | A card with no repos had gained a longer tooltip. The old XAML's `StringFormat` on a `ToolTip` is ignored by WPF, so the board always showed the bare branch; the pass-1 code and its test copied the format string instead of what was shown. Now the bare branch, as before |
| F2 | A card's repos are found once per refresh and used for both the PR links and the branch line (`PullRequestResolver.Resolve` takes them). Network folders (`\\server\...`) are skipped without being touched; such a card behaves as one with no repos there |
| F3 | A repo is counted once by its real location (`ResolveLinkTarget`), so a link or junction to it does not add a second |
| F5 | Origins are compared without scheme, user, `.git`, trailing slash or case, and with `host:path` read as `host/path`, so one origin written two ways links |
| F6 | Tests added: a working directory that is a repo is not shown when the card declares others (AC 4); a worktree with a relative gitdir; a feature folder's repos listed by name (they were in file-system order; now sorted) |
| F9 | A PR row that cannot be opened says why ("in one of api, ui") on its second line, and keeps a reported title as the title. Jira rows still point to Tools > Options |
| F10 | Recorded here: for a card that lists one repo, PR links now go to that repo's origin rather than the working directory's. This is the spec's intent (AC 11 reads declared repos) but was missing from the list of differences above |

Tests: new files `tests/Theseus.Tests/Domain/CardBranchesFixTests.cs` (10 tests) and
`tests/Theseus.Tests/ViewModels/CardItemRowNoLinkTests.cs` (2). They failed to build before the change. **One existing test
changed:** `BranchLinesTests.NoReposDeclared_ReadsTheWorkingDirectoryAsBefore` asserted the sentence WPF never showed,
which was the F1 defect; it now asserts the bare branch. Full suite **1520 of 1520**, 0 warnings.

Not fixed, recorded: a mapped network drive (a letter, not `\\`) is read like a local folder and can still be slow;
the repo name keeps the case of the first path given (F7); "on branch not readable" for a single unreadable repo (F4).

**After the re-verify (`evidence.md`, attempt 2, PASS), accepted by Nigel as known limits (2026-11-19):** a junction on a
folder above a repo still counts it twice (F3); ports, Azure DevOps https against ssh, and visualstudio.com against
dev.azure.com still count as two origins, so those PRs stay unlinked (F5), and a case-sensitive git server could merge
two repos whose names differ only in case; no tests for the App wiring or for `CardItemRows` passing the note (F6); a
local path written `\\?\...` is skipped like a network path (N1); an untitled unlinkable PR shows just "PR 42" in the
menu, with the repos in its tooltip (N2). Stopped here by method rule P10 rather than a third fix pass.
