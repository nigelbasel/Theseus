# Method kit v0

A small, versioned set of plain files that says how agentic work is run: which stages a piece of work
goes through, who does each, what each produces, and what has to be true before it moves on. Theseus is
built with it (the bootstrap) and will run it for others.

**Version:** v0, stage 0 of the Theseus bootstrap. Expect it to change after the five hand-run changes.

## What is in it

| File | What it defines |
|---|---|
| [`project.md`](project.md) | The project format: objective, phases, derived objectives, exit gates, workstreams, repos |
| [`pipelines/freeform.yaml`](pipelines/freeform.yaml) | Ad hoc work: one stage, no gates. Today's Theseus exactly |
| [`pipelines/feature.yaml`](pipelines/feature.yaml) | research → spec → plan → implement → verify → review → compound |
| [`pipelines/bug.yaml`](pipelines/bug.yaml) | reproduce (failing test first) → fix → verify |
| [`rubrics/reviewer.md`](rubrics/reviewer.md) | What a reviewer checks a Theseus change against, each item traced to a dated decision |

## Two modes

- **Standalone (ad hoc).** A card, a session, a handoff. Runs `freeform`. No project file, no artefacts
  folder, no gates, not one more click than Theseus has always needed.
- **Project.** A project file in `projects/` sets an objective and phases. Each workstream serves one
  derived objective and runs a pipeline, with gates and evidence. Evidence is always shown when it exists;
  only project gates block on it.

Ad hoc work can be promoted to a project. Its handoff becomes the project's first document.

## Rules that hold everywhere

- **Plain files, in the repo.** Pipelines, projects and artefacts are text that is reviewed and versioned
  like code.
- **Nothing assumes one repo.** A workstream may change several repos on one branch name, with a worktree
  per repo. Artefacts are per workstream, not per repo (see `project.md`).
- **Autonomy is per repo**, and a workstream runs at the lowest level any of its repos allows. Levels:
  L0 Assist, L1 Prepared, L2 Unattended to PR, L3 Gated merge. Stage 0 runs everything at L0: a person
  starts and accepts every stage.
- **The implementer never judges its own work alone.** Verification includes holdout scenarios the
  implementer cannot see or change, and review is done by a different session or person.
- **Never invent a figure.** Write a bracketed placeholder and list it.

## Decisions recorded here

| Date | Decision | By |
|---|---|---|
| 2026-11-18 | The kit lives in the Theseus repo under `method/` for now; it may move to its own repo later | Nigel |
| 2026-11-18 | Work artefacts go in top-level `work/<issue>/`, retrospectives in `bootstrap/`, both outside the `docs/` Pages site | Nigel |
| 2026-11-18 | Holdout scenarios live in a separate private repository, which implementing sessions are refused access to | Nigel |
| 2026-11-18 | Projects sit above workstreams; standalone workstreams are unchanged; both may span repos | Nigel |

## Open questions

- **Setting up the method.** Proposed (not decided): ad hoc work needs no setup; authoring a repo's
  AGENTS.md, a project file or a pipeline is done by an agent started from Theseus, reviewed as a pull
  request, rather than in a form; Theseus's UI is for seeing where work is, its evidence, and approving
  gates and promotions; Theseus reports malformed method files rather than ignoring them.
- **Where the kit lives long-term:** here, or in its own repository distributed as a plugin.
- **L1 Prepared:** [definition not yet written].
