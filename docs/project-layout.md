# How your work is laid out

Theseus keeps three kinds of thing apart, each in a place of its own. You choose the two folders in **Tools >
Options**; Theseus suggests `Documents\Theseus\projects` and `Documents\Theseus\work` but sets neither for you.

Theseus reads the projects folder. It does not make worktrees yet: the worktrees folder records where yours go, and
the layout below is the one to follow when you make them.

```
<projects folder>\
  └── <project>\                     one folder per project: its home
        ├── project.md               objectives, criteria, primary branch, the workstreams table
        └── work\<slug>\             one folder per workstream: research, spec, plan, implementation,
                                     evidence, review, lessons

<your source clones>\<repo>\         wherever you keep them; Theseus never moves them

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
| Source clones | `D:\src\github\acme\shop-api`, `D:\src\github\acme\shop-web` |

Their project `checkout-redesign` changes both repos. Its primary branch is `dana/checkout-redesign`, and one of its
workstreams has the slug `payment-step`:

```
D:\projects\checkout-redesign\project.md
D:\projects\checkout-redesign\work\payment-step\spec.md
D:\work\payment-step\shop-api\        worktree on dana/payment-step
D:\work\payment-step\shop-web\        worktree on dana/payment-step
```

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
