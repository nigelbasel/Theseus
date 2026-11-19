# Method kit v0

A small, versioned set of plain files that says how agentic work is run: which stages a piece of work
goes through, who does each, what each produces, and what has to be true before it moves on. Theseus is
built with it (the bootstrap) and will run it for others.

**Version:** `method-v0.1` (2026-11-19), the kit as it stood at the end of stage 0 of the Theseus bootstrap, after six
hand-run changes and 17 lessons.

## What is in it

| File | What it defines |
|---|---|
| [`project.md`](project.md) | The project format: objective, phases, derived objectives, exit gates, workstreams, repos |
| [`pipelines/freeform.yaml`](pipelines/freeform.yaml) | Ad hoc work: one stage, no gates. Today's Theseus exactly |
| [`pipelines/feature.yaml`](pipelines/feature.yaml) | research → spec → plan → implement → verify → review → compound |
| [`pipelines/bug.yaml`](pipelines/bug.yaml) | reproduce (failing test first) → rule (only when the fix needs one; owner's gate) → fix → verify, then review and compound when the rule stage ran |
| *(per repo)* | Each repo keeps its own reviewer rubric, drawn from that codebase's dated decisions, and names it in its `AGENTS.md`. The kit has none: a rubric is the repo's, not the method's. Theseus's is `.agents/reviewer-rubric.md` |

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
- **Stage times are measured, never estimated.** A stage's start comes from a log line written when it starts, its
  end from the commit that closes it. A time that was not captured is written "[not recorded]". (stop-timing P1;
  agents asked to record times wrote estimates twice in stage 0.)
- **A sub-agent's brief carries the owner's standing rules it needs:** no deletes of any kind (temp files
  included), the exact files it may write, no tracker writes, no commits or pushes, and the holdout's
  off-limits. A sub-agent does not inherit the rules its parent holds. (stop-timing P2.)
- **Stop patching when the edges keep coming.** When fix passes keep uncovering the next edge in the same place (a
  third pass is the signal), stop: record what is known as limits and propose a feature with a spec instead.
  (resumed-stop P10; guard-mentions and resumed-stop both took three passes.)
- **New tests go in a new file,** never beside a reproduction test, so "the reproduction test is unchanged since it
  failed" stays a plain diff check. (resumed-stop P9.)
- **Never rewrite a text file by reading it with Windows PowerShell's `Get-Content` and writing it back.** Version 5.1
  reads UTF-8 without a BOM as ANSI, so the write-back double-encodes every non-ASCII character. Use the editor tool, or
  .NET with an explicit UTF-8 encoding. Sub-agent briefs carry this rule. (parked-cores P14.)
- **Text a user acts on is checked against its source,** not against the code that prints it: an instruction, the name
  of a setting, a cause to check. Compare it with the spec or the vendor's documentation. (parked-cores P17; twice in one
  day a test asserted the implementer's own wrong words.)
- **A named holdout scenario is run, or verify stays open.** Verify cannot pass while a holdout scenario the spec names
  has not been run by an independent verifier. If nobody can run it, the stage stays open, so the gap shows instead
  of being passed over. The owner is not the relay between sessions: running scenarios is the verifier's job. (P18,
  Nigel, 2026-11-19; in stage 0 every spec named scenarios and none was run.)
- **An existing test changes only to correct a wrong expectation.** The change gets its own line in
  `implementation.md` saying what the test asserted and why that was wrong, and the verifier judges whether it is a
  correction or a weakening. (card-branches P12.)

## Decisions recorded here

| Date | Decision | By |
|---|---|---|
| 2026-11-18 | The kit lives in the Theseus repo under `method/` for now; it may move to its own repo later | Nigel |
| 2026-11-18 | Work artefacts go in top-level `work/<issue>/`, retrospectives in `bootstrap/`, both outside the `docs/` Pages site | Nigel |
| 2026-11-18 | Holdout scenarios live in a separate private repository, which implementing sessions are refused access to | Nigel |
| 2026-11-18 | Projects sit above workstreams; standalone workstreams are unchanged; both may span repos | Nigel |
| 2026-11-19 | The kit stays generic in `method/` on GitHub for now, ships with Theseus as the default, and moves only if other team members start using it. A repo's own rubric and AGENTS.md live in that repo | Nigel |

## Open questions

- **Setting up the method.** Proposed (not decided): ad hoc work needs no setup; authoring a repo's
  AGENTS.md, a project file or a pipeline is done by an agent started from Theseus, reviewed as a pull
  request, rather than in a form; Theseus's UI is for seeing where work is, its evidence, and approving
  gates and promotions; Theseus reports malformed method files rather than ignoring them.
- **Where the kit lives long-term:** here for now (decided 2026-11-19); revisit when other team members use it.
- **Pinning, by hand for now:** a project names the kit version it follows, as a `Kit:` line in its project file, so
  a change to the kit does not reach a running project unannounced. Versions are git tags `method-v<major>.<minor>`
  in this repo, separate from app releases; the first is `method-v0.1` (2026-11-19). A project moves to a newer
  version deliberately. Theseus does not yet read the `Kit:` line.
- **L1 Prepared:** [definition not yet written].
