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

**Project > New project…** asks for a name (lower-case letters, digits and hyphens, such as `checkout-redesign`),
makes its folder in the projects folder, and puts in a `project.md` to fill in and an empty `work` folder. The project
card appears on the board once its `project.md` reads. See [Cards](cards.md#projects) for what the project card and
the project view show.
