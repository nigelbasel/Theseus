# card-branches: spec

Pipeline: `feature` (method kit v0), stage **spec**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Scope: option B of `research.md`, with a card's repos taken from its extra directories (`addDirs`), both chosen by
Nigel on 2026-11-18.

## Goal

A card knows which repositories it covers and shows the branch of each, so that a workstream spanning repos (one
branch name, a worktree per repo) is visible as one, and a repo on the wrong branch stands out. A standalone card that
declares no repos looks exactly as it does today.

## Acceptance criteria

**Which repos a card covers**

1. A card's repos are found from its extra directories, in the order given. An extra directory that is inside a git
   repository (a clone or a worktree) contributes that repository. An extra directory that is not inside one, but
   whose immediate subfolders are repositories (a feature folder, `<root>\<feature>\`), contributes each of those. A
   repository is counted once, however many directories lead to it.
2. A directory that does not exist, or that leads to no repository, contributes nothing and causes no error.
3. If the extra directories give no repository, the card behaves exactly as today: it shows the branch of its working
   directory's repository, worded as today. (The 18 Nov decision: standalone work is unchanged.)
4. If they give at least one, the card shows those repositories' branches, and not the working directory's. A card
   that wants its working directory's repository shown lists it among its extra directories too.

**What the card shows**

5. One repository: "on `<branch>`", as today. Its tooltip names the repository and the branch.
6. Several, all on the same branch: "on `<branch>`" and the number of repositories, for example
   "on `<user>/<feature>` · 3 repos". The tooltip lists each repository and its branch.
7. Several, on different branches: the card marks it as a mismatch (for example "branches differ"), and the tooltip
   lists each repository and its branch, so the odd one out can be seen. Nothing else changes: no action is taken.
8. A repository with a detached head is shown as today ("detached at `<commit>`") and counts as differing from the
   others.
9. The line never grows with the number of repositories (rubric R18): the list is in the tooltip, not on the card.
10. Branches are read when they are read today (each board refresh), from the repository's own files, without running
    git.

**Pull-request links**

11. When a card's repositories have one origin between them, PR numbers link as today. When they have more than one,
    a PR number is shown without a link, rather than linked to a repository it may not belong to; its tooltip says
    which repositories it could be in.

**Unchanged**

12. How sessions are launched and which directories they are given; everything about a card that declares no repos;
    every other part of the card.
13. `docs/cards.md` describes the branch line as it now behaves.

## Out of scope

- Inferring a card's repos from what its session touched (option C).
- A separate "Repos" field, and any change to how `addDirs` are edited.
- Acting on a mismatch (switching branches, warning a session): the card only shows it.
- Repositories nested more than one level below a feature folder.

## Holdout scenarios that apply

`resume-tomorrow`, its multi-repo variant R2 (by name only).

## How it will be verified

- Unit tests in `Theseus.Core` for: finding a card's repos from its directories (a clone, a worktree, a feature folder,
  a missing folder, the same repo twice, none); the branch line's text for one repo, several on one branch, several on
  different branches, and a detached head; and the PR-link decision for one origin and several.
- The full suite.
- On a running board, by someone other than the implementer: a scratch card whose extra directory is a feature folder
  holding two worktrees of scratch repositories on the same branch, then with one switched to another branch; and a
  card with no extra directories, which must look as it did.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| spec | 2026-11-18 20:58:23 (logged) | this commit's time | Gate: Nigel approves this spec |
