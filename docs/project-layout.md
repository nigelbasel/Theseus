# How your work is laid out

Theseus keeps three kinds of thing apart, each in a place of its own. You choose the folders in **Tools >
Options**; Theseus suggests `Documents\Theseus\projects` and `Documents\Theseus\work` but sets none for you.

Theseus reads the projects folder. With the worktrees folder and the clones folder both chosen, it also makes each
workstream's worktrees when its card starts: see [Worktrees made at the start](#worktrees-made-at-the-start).

```
<projects folder>\
  └── <project>\                     one folder per project: its home
        ├── project.md               objectives, criteria, primary branch, the workstreams table
        └── work\<slug>\             one folder per workstream: research, spec, plan, implementation,
                                     evidence, review, lessons

<clones folder>\<host>\<owner>\<repo>\  your source clones, placed by their remote; Theseus never moves
                                     or makes them

<worktrees folder>\<slug>\<repo>\    one worktree per workstream per repo, on branch <you>/<slug>,
                                     made off the project's primary branch
```

| Place | Its job | Who writes it |
|---|---|---|
| **Project home** | What the work is and where it has got to | You, and the sessions working on the project |
| **Source clones** | The code as it is upstream, on its default branch | Nobody: pull only |
| **Worktrees** | The code each workstream is changing | The workstream's session |

## Why records are not in your code repos

A workstream's records (its spec, its plan, its evidence) are read by you, by the board and by other sessions while the
work is still going on. Kept in a code repo, each one would sit on the workstream's branch, invisible from any other
branch until it was merged. Kept in the project home they are plain files: every session sees the same copy the moment
it is written, and the board shows it within a couple of seconds. They never need a branch, a merge or a pull request,
and nothing about your process ends up in a repository other people clone.

Theseus does not put the projects folder under version control for you. A local git repository with no remote is a
good choice: you keep every version of every spec, and nothing in it leaves your machine.

## An example

A developer keeps everything on a `D:` drive:

| Setting or place | Path |
|---|---|
| Projects folder | `D:\projects` |
| Worktrees folder | `D:\work` |
| Clones folder | `D:\src` |
| Source clones | `D:\src\github\acme\shop-api`, `D:\src\github\acme\shop-web` |

Their project `checkout-redesign` changes both repos. Its primary branch is `dana/checkout-redesign`, and one of its
workstreams has the slug `payment-step`:

```
D:\projects\checkout-redesign\project.md
D:\projects\checkout-redesign\work\payment-step\spec.md
D:\work\payment-step\shop-api\        worktree on dana/payment-step
D:\work\payment-step\shop-web\        worktree on dana/payment-step
```

## Worktrees made at the start

When a card in a project starts, Theseus reads its row in `project.md`. If the row's Branch is a branch of its own (not
`none`, and not a trunk such as `main`), Theseus gets a worktree ready for each repo in the row's Repos before the
session opens:

- **The clone is found by the layout rule.** `github/acme/shop-api` is `<clones folder>\github\acme\shop-api`. A short
  name such as `shop-api` is found through the project's Repos table, or by searching the clones folder for the one
  clone with that name or remote. If there is no clone, or more than one, the card does not start and the status bar
  says why. **Theseus never clones.**
- **The worktree is `<worktrees folder>\<slug>\<repo>`, on the row's Branch.** A branch you already have is used as it
  is. One that is only on the remote is checked out tracking it. A new one is made off the project's primary branch
  where the repo has it, and otherwise off the repo's default branch. Theseus does not fetch, push or change any branch.
- **One already there is used.** A worktree at that place on the right branch is taken as it is. One on another
  branch, a folder in the way, or the branch checked out somewhere else stops the start, saying which.
- **Every repo is checked before anything is made**, so a refused start leaves nothing half made.
- **Worked in place.** A repo whose Role in the Repos table says "worked in place" gets no worktree. Its clone is
  given to the session as it is, and the status bar says if it is on another branch.
- An entry that names the project's home (a Repos row whose Role begins with "home") is left alone. So is prose such
  as "read-only", and anything in parentheses.

The folders are added to the card's extra directories. The status bar says how many were made and how many were
already there, and the session is told each folder and its branch when it opens.

### When the card is archived

Archiving a card whose slug folder holds its worktrees asks, as a checklist, which to remove. Only clean worktrees on
the card's branch are listed. One with uncommitted changes or untracked files is named and kept, and so is one another
card still uses. Removing a worktree never deletes its branch: every commit stays. Cancel removes nothing; the card is
archived either way.

## Starting a project

**File > New project…**, or the toolbar's New project button beside New workstream, asks for a name (lower-case
letters, digits and hyphens, such as `checkout-redesign`), and optionally a title, the objective and the primary
branch. It makes the project's folder in the projects folder, with a `project.md` filled in from what you gave and an
empty `work` folder, and its project card appears on the board.

A workstream card's right-click menu has **Add to project…** (a row naming it under one of a project's objectives),
**Remove from project** (its rows go; its records stay), and **Convert to project…** (a new project with the card as
its first workstream, and its handoff copied in as the first record). Each changes `project.md` itself, so what you see
is always what the file says, and a session can make the same changes by editing the file. See
[Cards](cards.md#projects) for the project card and the project view.
