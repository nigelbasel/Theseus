# card-branches: review

Pipeline: `feature` (method kit v0), stage **review**, autonomy L0. Repo Theseus, branch `main`, change
`git diff 5cb40df 38ea687 -- src tests docs`. Reviewer: a fresh sub-agent started by the implementer. It did not
write or verify the code, but it was briefed by the implementing session, so independence is partial.

Contracts: `work/card-branches/spec.md` (AC 1 to 13), `method/rubrics/reviewer.md` (R1 to R19), `AGENTS.md`
conventions, and the repo being public. Method: the diff and the touched files at `38ea687` read directly; callers of
`SyncPullRequests` and `Resolve` traced. No build, no tests run (verification did both). The limits Nigel accepted on
2026-11-19 (end of `implementation.md`) are not re-reported.

## Rubric

| Item | Verdict | Notes |
|---|---|---|
| R1 to R15 | not touched | No MCP tool, report, link, lock, launch, startup, settings or update code changed. AC 12 (launching and directories unchanged) holds. |
| R16 Conventional Windows behaviour | holds | No command added or moved. The branch line keeps a tooltip (`MainWindow.xaml:709`), now one that says something for multi-repo cards. |
| R17 Live state applies; setup in dialogs | not touched | Nothing editable added; `addDirs` editing unchanged (out of scope in the spec). |
| R18 A card does not grow without bound | holds | One `TextBlock`, `MaxWidth="220"` with `CharacterEllipsis` (`MainWindow.xaml:708-709`); the per-repo list is only in the tooltip (`BranchLines.cs:33`). PR rows stay in the existing drop-down. |
| R19 Lock colours | not touched | |

## Findings

| # | Severity | Where | Finding | Rests on |
|---|---|---|---|---|
| 1 | should fix | `src/Theseus.Core/Domain/PullRequestResolver.cs:33-62` | When a card declares repos but none of them has a readable origin (`origins.Count == 0`), the code falls through to `Candidates`, which tries the **working directory first** and then every extra directory. So a card covering local-only repos, whose working directory is a repo with an origin, links its PR numbers to the working directory's repository: one that is not among the card's repos. That is the guessed link AC 11 exists to prevent, and AC 4 says declared repos replace the working directory. No test covers this branch. Either give no link (with a note) or record it as an accepted limit. | AC 4, AC 11 |
| 2 | should fix | `src/Theseus.App/ViewModels/WorkstreamCardViewModel.cs:168` | `SetItems` replaces the PR rows only when `Display + Url` changes. The new `whyNoLink` note is in neither, so when a card's repos change (a third repo added to the feature folder), an unlinked PR keeps saying "in one of api, ui" until something else about the row changes. The comparison should include the tooltip (or the note). | AC 11 ("its tooltip says which repositories it could be in") |
| 3 | consider | `tests/Theseus.Tests/Domain/CardBranchesFixTests.cs` | The file is named after a fix pass, not a subject, and mixes tests of `BranchLines`, `CardRepos` and `PullRequestResolver`, which a future reader will look for in the subject's own fixture. Its first test (`:33-38`) repeats `BranchLinesTests.NoReposDeclared_ReadsTheWorkingDirectoryAsBefore` (`BranchLinesTests.cs:31-42`, the tooltip assertion at `:40`). Move the tests to their subjects' fixtures and drop the duplicate. The private `Remotes` fake is also copied verbatim in `PullRequestResolverRepoTests.cs:78-87`, which moving would leave in one place. | AGENTS.md (a test proves one behaviour; simplicity) |
| 4 | consider | `tests/Theseus.Tests/Domain/PullRequestResolverRepoTests.cs:29-39` | `ReposWithOneOriginBetweenThem_LinkAsBefore` would pass on the code before this change too (the old `Candidates` walk reaches `api` after the working directory has no origin). It is a regression guard, not evidence for AC 11's one-origin branch; `CardBranchesFixTests.OneOriginWrittenTwoWays_IsOneOrigin` is what actually discriminates. Making the working directory a repo with a different origin would make it bite. | Tests that cannot fail |
| 5 | consider | `src/Theseus.Core/Domain/PullRequestResolver.cs:65-88` | `OriginKey` is a second parser of remote URLs beside `PullRequestSite.Normalise`. What matters for AC 11 is whether every origin produces the same pull-request address, so comparing `PullRequestSite.Url(origin, n)` case-insensitively (falling back to the trimmed URL when it is null) would reuse the existing parser and drop about twenty lines. Its doc comment also says "without ... port separator" but keeps an explicit port (the verifier noted this too). | Simplicity; comments say what is true |
| 6 | consider | `src/Theseus.Core/Domain/PullRequestResolver.cs:20, 31` | The optional `repos` parameter exists so tests can omit it; the one production caller (`ShellViewModel.cs:1198`) always passes it. `null` meaning "find them" and `[]` meaning "none" is easy to misread. A required parameter, with tests passing `CardRepos.Find(...)`, would be smaller and say what happens. | Simplicity (speculative flexibility) |
| 7 | consider | `src/Theseus.Core/Domain/GitRepository.cs:19-31` | A third upward walk for `.git` now sits beside `GitBranchReader.GitDirectory` and `GitRemoteReader.FindConfiguration`. Recorded in `implementation.md` as deliberate; the two readers could start from `GitRepository.RootOf` instead. Not needed for this change. | Simplicity (duplication) |
| 8 | consider | `src/Theseus.Core/Domain/BranchLines.cs:36` | Detection of a detached head matches the literal `"detached at "` that `GitBranchReader.Parse` writes. A change to that wording would silently make two detached repos at one commit read as "on detached at ... · 2 repos". A flag on `CardRepo`, or one shared constant, would tie them. | AC 8 |
| 9 | consider | `src/Theseus.App/MainWindow.xaml:672-677` | The comment above the drop-downs still says the line shows "the branch the working directory has checked out", which is now true only for cards declaring no repos. | AGENTS.md (comments say why, and stay true) |
| 10 | consider | `docs/cards.md:48` | The PR row in the card table is unchanged, so the help does not say that a PR on a card whose repos have several origins is listed without a link, and why. AC 13 asks only for the branch line, so this is not a breach, but a user seeing an unopenable PR has nothing to read. | AC 11, AC 13 |
| 11 | consider | `src/Theseus.Core/Domain/CardRepos.cs:38, 43`; `BranchLines.cs:19-20`; `CardItemRow.cs:13-14`; `PullRequestResolver.cs:35` | Comments cite findings ("pass 2, F2", "F3", "F9", "verify part 1, F1") rather than dated decisions. The prose around them does say why, so the citations are only a pointer into `work/card-branches/`; a date ("card-branches verify, 2026-11-19") would match the convention. | AGENTS.md (cite the decision, dated) |
| 12 | consider | `src/Theseus.App/ViewModels/CardItemRow.cs:44` | The new field sits between two properties rather than with the constructor it is set in. | Readability |
| 13 | consider | `work/card-branches/implementation.md`, "What was done" (`CardItemRows.cs` row) and the first bullet of "Every difference from the plan" | Both still say the PR note is shown as the row's title; pass 2 (F9) moved it to the tooltip's second line. The Pass 2 section is correct; the earlier lines now misdescribe the code. | Record accuracy |

### Checked and holding

- **AC 3 / AC 12, a card declaring no repos.** `CardRepos.Read` returns empty, `Resolve` is passed `[]` so it does not
  search again and goes straight to the old `Candidates` path (`PullRequestResolver.cs:58-62`, unchanged), and
  `BranchLines.For` returns `on {branch}` with the bare branch as tooltip (`BranchLines.cs:17-24`), which is what WPF
  showed before (a `StringFormat` on a `ToolTip` binding is ignored). Visibility and colour are unchanged.
- **AC 1 / AC 2.** Repo inside a directory, worktree `.git` file, feature folder's immediate children, missing or empty
  folders, duplicates by real location, order first reached: all in `CardRepos.Find` and tested.
- **AC 5 to 9.** Wording matches the spec; detached and unreadable heads count as differing only among several.
- **AC 10 and the UI thread.** `SyncPullRequests` runs synchronously on the UI thread (`ShellViewModel.cs:631`, inside
  `Refresh`, and from `App.xaml.cs:121` via the dispatcher), as before. The added work is a handful of file-system
  checks per declared directory, plus one directory enumeration for a feature folder; UNC paths are skipped before any
  file-system call. Nothing runs git. A mapped network drive is still read on the UI thread, which is an accepted limit.
  A very large non-repo folder given as an extra directory would be enumerated on every refresh; not seen as a
  practical risk.
- **Public repo.** No internal names, keys, secrets or machine paths in code, tests or docs; tests use `api`, `ui`,
  `owner/app` and a `.invalid` host.
- **One type per file, warnings, test names.** Each new type has its own file; the test fakes are private nested
  types; test names are sentences.

## Summary

The change meets the spec on every AC I traced. Cards that declare no repos behave exactly as before, which was the
point the owner cared most about. R16 and R18 hold, and nothing else in the rubric is touched. There is **no must-fix**.
Two findings should be fixed or explicitly accepted before the change is taken as done. First, declared repos with no
readable origin still fall back to linking PRs to the working directory's repo (finding 1). Second, the unlinked-PR
tooltip can go stale because `SetItems` does not compare it (finding 2). Both are small. The rest are simplifications
and tidying that can wait. **It can be accepted as is only if the owner accepts findings 1 and 2 as known limits;
otherwise it needs one small pass.**

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| review | 2026-11-19 08:53:26 +0000 (clock) | 2026-11-19 08:56:37 +0000 (clock) | Read only; this file is the only one written |

## What was done with each finding (Nigel chose option A, 2026-11-19)

| Finding | Disposition |
|---|---|
| Should-fix 1 | **Fixed.** Declared repos with no readable origin give no link and the note "<repos> have no remote to link to"; the working directory is not asked (AC 4, 11). Test `DeclaredReposWithNoOrigin_GiveNoLink_EvenWhenTheWorkingDirectoryHasOne` failed (linked to the working directory's repo) before the fix |
| Should-fix 2 | **Fixed.** `SetItems` compares the tooltip as well, so a changed reason reaches the card. Test `APullRequestRowWhoseReasonChanged_IsReplacedOnTheCard` failed (old reason kept) before the fix |
| Stale text | **Fixed:** the `MainWindow.xaml` comment, `docs/cards.md` (unlinked PRs explained), and the superseded line in `implementation.md` |
| Other "consider" items | Accepted as they are |

Tests in a new file, `tests/Theseus.Tests/Domain/CardBranchesReviewFixTests.cs`; no existing test file changed. Full suite
1522 of 1522, 0 warnings. No new live check: neither case can be set up by hand cheaply, and both are tested end to end
in Core and the card view model.
