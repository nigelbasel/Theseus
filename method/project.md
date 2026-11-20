# The project format

A project is a folder, its **home folder**, holding `project.md` (this file's shape) and `work/<slug>/`, the records
of each workstream. Home folders sit side by side in one **projects folder**, outside every code repo, so records
never need a branch, a merge or a pull request, and every session sees the same copy at once. A new project starts
from the files in `method/starter/`. It is read by people and by agents, so it is prose with a fixed shape,
not a schema to fill in. Standalone (ad hoc) workstreams have no project file.

From `method-v0.2` the shape below is also read by Theseus, which shows a project's phases, objectives and workstreams
from it. The lines and headings it reads are fixed; everything else is prose and is left alone. A file that does not
follow the shape is shown as unreadable, with the line and the reason, never guessed at.

## Shape

```markdown
# <Project title>

**Kit:** method-v0.2
**Primary branch:** <user>/<project>

## Objective
One paragraph: the outcome the whole project exists for.

## Acceptance criteria
- [ ] Observable facts that mean the objective is met. Evidence, not dates.

## Repos
| Repo | Role | Autonomy |
|---|---|---|
| <host>/<owner>/<repo> | home | L0 |
| <host>/<owner>/<other-repo> | changed by phase 2 | L0 |

## Phase <n>: <name>
**Exit gate:** who decides, on what evidence.

### Objective <n>.<m>: <title>
What this part of the work must achieve.
- [ ] Its acceptance criteria.

**Pipeline:** feature | bug | freeform
| Workstream | Slug | Branch | Repos |
|---|---|---|---|
| <plain name> | <slug> | <user>/<slug> | <repo>, <other-repo> |

## Decisions
| Date | Decision | By |
```

## What is read

| Line | Rule |
|---|---|
| `# <title>` | The first heading of the file |
| `**Kit:** <version>` | Under the title. The kit version the file follows. A file at `method-v0.1` is shown as pinned to it and not read further |
| `**Before method-v0.2:** <slug>, <slug>` | Optional, under the title. Workstreams finished before the project moved to `method-v0.2`, shown at their stage with no gate markers |
| `**Primary branch:** <branch>` | Under the title. The project's long-lived branch, off the trunk: workstream branches come off it and merge back into it, and one pull request takes it into the trunk |
| `## Phase <n>: <name>` | A phase. Phases are numbered from 0 in order. Any other `## ` heading ends the phase |
| `**Exit gate:**` | The phase's exit gate. More than one line under a phase are read together, in order |
| `### Objective <n>.<m>: <title>` | Every objective is a heading of this form, numbered within its phase (`<n>` is the phase's number) |
| `- [ ]` and `- [x]` | Under an objective: its criteria, ticked when met. A criterion may run onto indented lines below it |
| `**Pipeline:** <name>` | Under every objective, once: `feature`, `bug` or `freeform`. An objective whose workstreams run different pipelines names them, `feature or bug`; each workstream's is then read from its artefacts (`reproduction.md` or `fix.md` means bug). `[to be chosen]` while no workstream serves the objective yet |
| `\| Workstream \| Slug \| Branch \| Repos \|` | Under every objective, once, after its pipeline: one row per workstream slug, and no rows while no workstream serves it |

In the workstreams table:

- **Workstream** is a plain name for the workstream, never a card id (P19). Several rows with the same name are one
  workstream that has worked under several slugs, one after another.
- **Slug** names the workstream's artefacts folder, `work/<slug>/`, and its branch, `<user>/<slug>`. It is lower-case
  letters, digits and hyphens, and appears once in the file.
- **Branch** is the branch its work is on until it is merged into the primary branch.
- **Repos** lists every repo it changes.

An objective is **done** when every criterion line under it is ticked; an objective with no criteria yet is not done.
The **current phase** is the first with an objective not done.

## Rules

- **The objective is the project's.** Phases break it down; each phase's derived objectives have their
  own acceptance criteria, and the phase has one exit gate. A phase is done on evidence, not on a date.
- **A workstream serves exactly one derived objective.** Several may serve the same one at once. They
  coordinate as Theseus already does: links for dependencies, locks for shared resources, and overlap
  detection on the files they change.
- **A workstream may span repos.** It uses one branch name in every repo it changes, with one worktree per
  repo, all under one feature folder: `<root>\<feature>\<repo>` on `<user>/<slug>`. Nothing in a
  pipeline or artefact path names a single repo.
- **Artefacts live once, per workstream, in the home folder**, under `work/<slug>/`, whichever repos the
  code changes are in. `<slug>` is the workstream's slug from the project file's table, the same name its branch
  carries, `<user>/<slug>`. A tracker item the workstream is for is named in its artefacts, not in the folder name.
- **Evidence is per repo.** Where a stage needs proof (tests passed, a scenario passed), it records one
  result for each repo the workstream changed. A repo with no result has no evidence. The verify stage's result is
  also written as a `theseus-evidence` block (below).
- **Autonomy is per repo.** The table under Repos sets each repo's level. A workstream runs at the lowest
  level of the repos it changes.
- **Decisions are recorded in the file**, dated and attributed, not only in a session.

## The owner's gates in a project

From `method-v0.2` the owner's gates inside a project are each workstream's **spec** (a bug's rule), and the primary
branch's **pull request into the trunk**, which passes when he merges it (Nigel, 2026-11-20, decision 4A). Every other
gate passes on its artefact or its evidence: plan and review are evidence gates inside a project
(`pipelines/feature.yaml`, `pipelines/bug.yaml`). The owner's acceptance of a spec is recorded by Theseus on the board,
by the content of the spec he accepted, not in a file a session can write.

## The evidence result block

`work/<slug>/evidence.md` carries the verify stage's result as a fenced block tagged `theseus-evidence`, holding
JSON; the rest of the file stays prose. Several blocks are allowed (part 1 and part 2, attempts): the **last** one in
the file is the current one.

````
```theseus-evidence
{
  "kit": "method-v0.2",
  "pipeline": "feature",
  "stage": "verify",
  "result": "open",
  "repos": [
    { "repo": "<repo>", "build": "pass", "tests": { "passed": "<n>", "total": "<n>" }, "warnings": "<n>" }
  ],
  "scenarios": [
    { "name": "<scenario>", "result": "not-run", "by": null }
  ],
  "gates": [
    { "gate": "spec", "accepted": null }
  ]
}
```
````

| Field | Rule |
|---|---|
| `result` | `pass` only when every repo passes and every named scenario was run by an independent verifier and passed (P18); otherwise `fail` or `open` |
| `repos[]` | One entry per repo the workstream changed: `build` (`pass` or `fail`), `tests` (passed and total, as numbers), `warnings` (a number) |
| `scenarios[].result` | `pass`, `fail` or `not-run`. `by` names who ran it: `verifier card`, or the owner |
| `gates` | Information only. A session can write this field, so it is a claim, never proof that a gate passed: the owner's acceptance is recorded on the board, and his merge of the primary's pull request into the trunk is the real gate (Nigel, 2026-11-20) |

The `<n>` in the example are placeholders for measured figures: a block records what was measured, never an estimate.

## Promotion

Ad hoc work becomes a project by adding the workstream, under a slug, to the table of the objective it serves. The
card joins the project on the board and keeps its key; its handoff is linked as the project's first document. Nothing
about the card's work changes until it next starts a stage of a pipeline.

## Example

[`starter/project.md`](starter/project.md), the file a new project begins with.
