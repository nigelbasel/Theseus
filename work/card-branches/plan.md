# card-branches: plan

Pipeline: `feature` (method kit v0), stage **plan**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Implements `spec.md` (approved by Nigel, 2026-11-18). One repo changed (Theseus); the feature is about cards that span
several.

## Design

Behaviour in `Theseus.Core`; the App only binds to it. One type per file.

| Type (new or changed) | File | What it does |
|---|---|---|
| `GitRepository` (new static) | `Core/Domain/GitRepository.cs` | `RootOf(path)`: the directory holding `.git` (a folder, or a worktree's `.git` file) at or above `path`, or null. Pure file-system reads, no git process. `GitBranchReader` keeps its own walk (not changed) |
| `CardRepo` (new record) | `Core/Domain/CardRepo.cs` | `Path`, `Name` (the folder name), `Branch` (as `GitBranchReader.Read` gives it, or null) |
| `CardRepos` (new static) | `Core/Domain/CardRepos.cs` | `Find(addDirectories)`: AC 1 and 2. For each directory in order: its `RootOf`; if none, its immediate subfolders that are repository roots; each root once (case-insensitive), first-seen order. `Read(addDirectories)`: `Find`, then each one's branch, as `CardRepo`s |
| `BranchLine` (new record) | `Core/Domain/BranchLine.cs` | `Text`, `Tooltip`, `Differs` |
| `BranchLines` (new static) | `Core/Domain/BranchLines.cs` | `For(workingDirectory, repos)`: no repos, then today's line from the working directory (AC 3); one, "on X" (AC 5); several on one branch, "on X · N repos" (AC 6); several differing, "branches differ" with `Differs` (AC 7, 8). Tooltips list "name: branch" per line (AC 9) |
| `PullRequestLink` (changed) | `Core/Domain/PullRequestLink.cs` | Gains optional `Note` (init), the tooltip when there is no link |
| `PullRequestResolver` (changed) | `Core/Domain/PullRequestResolver.cs` | When `CardRepos.Find(AddDirectories)` gives repos: read each one's origin; one distinct origin, link as today; more than one, `Url` null and `Note` naming the repos (AC 11). No repos: exactly today's path |
| `WorkstreamCardViewModel` (changed) | `App/ViewModels/WorkstreamCardViewModel.cs` | `Branch` keeps the text; new `BranchTooltip`, `BranchesDiffer` |
| `ShellViewModel` (changed) | `App/ViewModels/ShellViewModel.cs` | Line 1197: sets the three from `BranchLines.For(...)` instead of `GitBranchReader.Read(...)` |
| `MainWindow.xaml` (changed) | | The branch text binds to `Branch` directly (it is now worded in Core), its tooltip to `BranchTooltip`; a "differs" style (the theme's caution colour) when `BranchesDiffer`. PR tooltip falls back to `Note` when there is no `Url` |
| `docs/cards.md` (changed) | | The branch line row, per AC 13 |

Today the XAML formats "on {0}" from a bare branch name. Moving the wording into Core (`BranchLines`) is what makes it
testable; for a card with no repos the text and tooltip must come out identical to today's (AC 3, tested).

## Steps, tests first, each in a new file

1. `tests/Theseus.Tests/Domain/CardReposTests.cs`: a clone, a worktree (`.git` file), a feature folder with two repos, a
   folder inside a repo, a missing folder, the same repo through two folders, none. Built in a temp folder with
   hand-made `.git` folders and `HEAD` files. Fails before `CardRepos` exists.
2. `tests/Theseus.Tests/Domain/BranchLinesTests.cs`: no repos (identical to today for a repo and for a non-repo working
   directory), one, several same, several differing, a detached one among them.
3. `tests/Theseus.Tests/Domain/PullRequestResolverRepoTests.cs`: one origin across repos links; two origins give no
   link and a note; no repos is unchanged. Uses a fake `IGitRemoteReader`.
4. Implement Core, then the App wiring and XAML, then the docs. Full suite, 0 warnings.
5. `implementation.md` lists every difference from this plan.

## Risks

- **Changing the line's wording source** (XAML format string to Core text) could change what a standalone card shows.
  Covered by step 2's "identical to today" tests, and by the live check of a card with no repos.
- **Reading `HEAD` for several repos per card on each refresh.** At most a few small file reads per card that declares
  repos; cards with none do what they do today.
- **The App binding and the "differs" style have no unit tests;** the live check covers them.
- **Feature folders with many subfolders**: only the immediate subfolders are checked, for a `.git` entry.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| plan | 2026-11-18 20:59:42 (logged) | this commit's time | Gate: Nigel approves this plan |
