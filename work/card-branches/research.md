# card-branches: research

Pipeline: `feature` (method kit v0), stage **research**, autonomy L0. Branch `main`, repo Theseus.
No code changes in this stage.

## The ask

A card should show the branch of every repo it covers, not only its working directory's. The first real test of
the 18 Nov 2026 rule: a workstream may span repos, on one branch name, with a worktree per repo, and nothing may
assume one repo.

## What exists today

| What | Where | How it decides |
|---|---|---|
| The branch on a card | `ShellViewModel.cs:1197`: `GitBranchReader.Read(card.Workstream.WorkingDirectory)` | The working directory's repository only, found by walking up to a `.git` folder or file (worktrees handled). The card has one `Branch` string. `docs/cards.md` promises exactly this: "the git branch checked out in the card's working directory" |
| Pull-request links | `PullRequestResolver` | PR numbers come from the card's prompt; **every** number is linked to the first of the working directory and the extra directories (`AddDirectories`) whose origin can be read. In a two-repo workstream, the second repo's PRs get the first repo's address |
| The repos a card covers | `Workstream.WorkingDirectory` and `Workstream.AddDirectories` (the card's `cwd` and `addDirs`) | Nothing else. Theseus has no other notion of which repos a workstream uses |

## What the real board shows (this machine, 2026-11-18, read only)

| Finding | Count |
|---|---|
| Cards | 87 |
| Cards whose working directory is the notes repo (`E:\claude-hq` or below), which is a git repo on `main` | **85** |
| Cards whose extra directories (`addDirs`) are inside a repo | 3 |
| Cards that span two or more repos through `addDirs` | **0** |

What this means:

- **On this board, 85 of 87 cards show the notes repo's branch (`main`), which says nothing about their work.** The
  repos a session actually works in come from the user's global settings (`additionalDirectories`, 39 folders given to
  every session), not from the card.
- **The cards do not say which repos they cover.** The owner's own convention ("a card lists its worktrees in
  addDirs") is followed by 3 cards. So showing every `addDirs` repo's branch, alone, would change almost nothing here.
- **No card on this board spans two repos yet.** The multi-repo case is real for the work (features touch more than one
  repo), but it is not yet visible to Theseus at all.

## Options

| Option | What it is | For | Against |
|---|---|---|---|
| A. Branches of the declared repos | The card shows one `repo: branch` per repository among its working directory and `addDirs`, and does not show the working directory's branch when that directory is not a workstream repo but a place notes live | Small; matches the convention and the 18 Nov rule; no guessing | Only as good as `addDirs`, which 3 of 87 cards fill. Needs a rule for "not a workstream repo" |
| **B. A, plus "Repos" as a first-class part of the card** (recommended) | The card's setup gains its repos (or one feature folder, `<root>\<feature>\`, from which the repos under it are found); the board shows each one's branch, and PR links resolve per repo | Makes the rule real: the card says what it covers, every surface uses it, and the board can warn when the repos are not all on the same branch name | Needs the owner to fill it in, though one feature folder does it in one field. Standalone cards must not need it: empty means "as today" |
| C. Infer the repos from the session | Read the transcript for the paths and `git` commands the session used, and show the branch of each repo it touched | No setup | A guess: misses repos not yet touched, picks up ones only read; harder to test; and the card would still not say what it is for |

## Recommendation

**B.** The finding is not that the card reads the branch wrongly, it is that **the card does not know its repos**. A
alone shows nothing new on this board; C guesses. B gives the card its repos, with one optional field (a feature
folder is enough), uses that for the branch line and for PR links, and leaves standalone cards exactly as they are when
it is empty. A mismatch warning ("repo X is on branch Y, not `nigel/feature`") falls out of it cheaply and is the check
the 18 Nov rule implies.

Two decisions for the spec, not the research:
- What to show for a card whose only directory is a notes folder with no repos declared: today's branch, or nothing.
- Whether `addDirs` stays the place repos are declared (the convention says so) or a separate "Repos" field is added,
  since `addDirs` also carries folders that are not repos.

## Constraints found

- The card's setup dialog has Save and Cancel and is "changed rarely and with care" (rubric R17); a new field belongs
  there, not in the drawer.
- A card must not grow without bound (rubric R18): several repos need a compact form (for example one line, or a
  drop-down like issues and PRs).
- The repo is public: examples use placeholders, never this board's card names or paths.
- `docs/cards.md` describes the branch line and must change with it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| research | 2026-11-18 20:52:44 (logged) | this commit's time | Read the card, branch and PR code; surveyed this board's 87 cards read only |
