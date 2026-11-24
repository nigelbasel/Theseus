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

## Rounds
| Round | Name | Began | Why | By |
|---|---|---|---|---|
| 1 | <name> | <yyyy-mm-dd> | The first requirements | <owner> |

## Repos
| Repo | Role | Autonomy |
|---|---|---|
| <host>/<owner>/<repo> | home | L0 |
| <host>/<owner>/<other-repo> | changed by phase 2 | L0 |

## Phase <n>: <name>
**Exit gate:** who decides, on what evidence.

### Objective <n>.<m>: <title>
**Round:** <n>
What this part of the work must achieve.
- [ ] Its acceptance criteria.
      Waiting: owner: <what, in plain words>

**Pipeline:** feature | bug | freeform
| Workstream | Slug | Branch | Repos | What |
|---|---|---|---|---|
| <card> | <slug> | <user>/<slug> | <repo>, <other-repo> | <what the piece achieves> |

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
| `## Rounds` | Optional, after Acceptance criteria. One row per round, numbered from 1: when the owner changed the requirements and work went back to research, its name, the date it began, why in one sentence, and who. No table: the file reads as one round, "Round 1" |
| `**Round:** <n>` | Optional, directly under an objective's heading: the round that added or reopened it. No line: round 1. A round not in the table makes the file need updating |
| `- [ ]` and `- [x]` | Under an objective: its criteria, ticked when met. A criterion may run onto indented lines below it |
| `Waiting: owner: <what>`, `Waiting: work: <what>`, `Waiting: on <who>: <what>` | Optional, an indented line under an unticked criterion: who it waits on, in plain words. An owner line puts it in the owner's "Waits for you"; `on <who>` names anyone else, such as the lead or another team, and is shown as theirs; no line means it waits on work. One line may list several, separated by `; `, as `Waiting: owner: A; work: B` |
| `Evidence: <pointer>` | Optional, an indented line under a ticked criterion: a one-line pointer to what shows it is met |
| `**Pipeline:** <name>` | Under every objective, once: `feature`, `bug` or `freeform`. An objective whose workstreams run different pipelines names them, `feature or bug`; each workstream's is then read from its artefacts (`reproduction.md` or `fix.md` means bug). `[to be chosen]` while no workstream serves the objective yet |
| `\| Workstream \| Slug \| Branch \| Repos \|` | Under every objective, once, after its pipeline: one row per workstream slug, and no rows while no workstream serves it. An optional fifth column, `What`, says in a few words what each piece achieves; it is the title its box shows. A What cell may be empty |

In the workstreams table:

- **Workstream** names the workstream's card, by its name on the Theseus board. The table alone puts a card in the
  project. Several rows naming the same card are one workstream that has worked under several slugs, one after another;
  its current slug is its last row. (Project files are no longer in a public repo, so P19 does not apply to them.)
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

**A session stops at its spec.** Once `spec.md` (a bug's rule) is written, the session says so and waits. Before it
plans it calls Theseus's `gates()`, which gives this workstream's gate and whether the version on disk is accepted,
with when, by whom and how, and it plans only when that version is accepted. The owner saying "accept" in the
session's tab is not the gate: the session reports it with `accept_spec(slug, version, owner_words)`, quoting him, and
Theseus asks him to confirm on the board; only then is it recorded, as said in that card's tab and confirmed on the
board. Whichever way he accepts, Theseus tells the session: a stopped card is started with "Your spec (version <v>) was
accepted: carry on to the plan", and a running one is told with its next turn. Every other board action that concerns
a session (a report to it confirmed, its request to start a card he paused allowed or refused) is told the same way, and
`events()` lists them, so a session never asks the owner to do what he has already done. A session calls `events()`
when it starts. Confirming a report to a stopped card of a project starts it, unless he paused, blocked or archived it.

**Sessions run the project's cards; the owner reviews objectives.** In a project a session creates the cards the work
needs with `propose_workstream`, naming an objective already in the file and a records folder; the card is made at once,
starts only with `start_now`, and needs no approval. Any session starts, stops, pauses and resumes any card of the
project but its own, with `start_workstream` and the rest; never another project's or an ad hoc card. A card the owner
paused waits for his Allow: the start is filed as a request, and `events()` tells the answer.

**Theseus writes the structured lines; sessions write the prose.** When a session in a project creates a card,
Theseus adds the new card's row under the objective it names, with the branch and repos it names
(none named: branch `none`, repos `read-only`, never invented). It writes a verifier card's row too. A proposal from a
project's card must name its objective. Every other line Theseus reads is written through its tools, never by hand,
so a session cannot leave the file unreadable:

| Tool | Writes |
|---|---|
| `record_round(name, why, objectives)` | A row of the Rounds table (making the table, with round 1, when there is none) and a Round line on each objective the round adds or reopens. It asks the owner in the tab first |
| `add_objective(phase, title, criteria, pipeline, waiting)` | An objective at the end of a phase, with its criteria, its pipeline line and an empty workstreams table. It asks the owner in the tab first |
| `tick_criterion(objective, criterion, text, ticked, evidence)` | A tick, with its Evidence line; unticking removes the Evidence line |
| `set_waiting(objective, criterion, text, who, what)` | A criterion's `Waiting:` line: `who` is `owner`, `work`, a name (written `on <who>:`), or `none` to remove it |
| `record_decision(decision, by)` | A dated row of the Decisions table |

Each is refused, with its reason, when the file changed since Theseus read it or the edit would not read. Prose
(descriptions, notes under a table, everything Theseus does not read) is written by hand.

## Rounds

When the owner changes the requirements and work goes back to research, a new **round** begins. The session that
records the change calls `record_round` with the round's name, why the requirements changed in one sentence in his
words, and the objectives the round adds or reopens. The project view lists rounds in order, each after a banner saying
when, by whom and why the requirements changed.

## Telling Theseus where you are

A session in a project calls `now_at(stage, doing)` **on entering each stage** of its piece of work, with what it is
doing in under 80 characters ("re-researching from the GUI's features"). The project view shows that stage busy, with
those words, until the next call; with no call it says the session "has not said on what", and guesses nothing.

**A stage may be skipped only where its pipeline says so** (each stage's `skip` in `pipelines/*.yaml`). A stage with
no `skip` may not be skipped. Say so with
`now_at(stage, "skipped: <reason>")`, or a line `<Stage>: <reason>` in `work/<slug>/skipped.md`. A feature's spec and
the verify stage are never skipped: only the owner's acceptance passes the first, and only the evidence and the
independent check pass the second. A bug's rule is written only when the fix needs one; with none, nothing needs
saying. A stage before the furthest recorded one that is neither recorded nor skipped
shows as "done, no record".

**Asking the owner.** A session that needs the owner's answer before it can go on asks with the question tool and
calls `ask_owner(question, options, why)` with the same question, so it shows on its card, in Waits for you and in
Needs you until he answers; once the question tool has his answer, it calls `ask_owner(answered: true)`. It never
asks only in prose: prose may explain the question, but a turn that ends with a question written only as text looks
finished on the board, and is shown only as a guess, "Probably asking you something".

**Giving work to another card.** A session that gives work to a card of its project that already exists, such as a
lead passing the bugs it found to the workstream that built that part, does it with `start_workstream` (or
`resume_workstream` for a card it paused) and an `instruction` saying the work, never by a message to that session.
Messages between sessions do not reach the board. The instruction does: the card's box shows "← <giver>: <first line>,
<time>", reopens at its build or fix stage while that session is open, and its Properties hold the whole
instruction. Its first line names the work on its own, because the box shows only that line. (The project lead, 2026-11-24.)

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

Ad hoc work joins a project by adding its card, under a slug, to the table of the objective it serves: by hand, or with
Theseus's **Add to project…**. The card joins the project on the board and keeps its key. **Convert to project…** makes
a new project with the card as its first workstream, and its handoff as the project's first record. Nothing
about the card's work changes until it next starts a stage of a pipeline.

## Example

[`starter/project.md`](starter/project.md), the file a new project begins with.
