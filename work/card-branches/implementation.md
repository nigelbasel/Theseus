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
