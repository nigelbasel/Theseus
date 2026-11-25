# Method kit v0

A small, versioned set of plain files that says how agentic work is run: which stages a piece of work
goes through, who does each, what each produces, and what has to be true before it moves on. Theseus is
built with it (the bootstrap) and will run it for others.

**Version:** `method-v0.2` (2026-11-20). `method-v0.1` (2026-11-19) was the kit as it stood at the end of stage 0 of
the Theseus bootstrap, after six hand-run changes and 17 lessons. `method-v0.2` adds, together:

- the **evidence result block**, a machine-readable `theseus-evidence` block in `evidence.md` ([`project.md`](project.md));
- **P18** (below): a named holdout scenario is run, or verify stays open;
- the **project file's shape tightened** so Theseus can read it: `Kit:` and `Primary branch:` lines, every objective a
  heading with its pipeline and one workstreams table, and the artefacts folder named by the workstream's slug;
- **the owner's gates inside a project** are each workstream's spec (a bug's rule) and the primary branch's pull request
  into the trunk; plan and review become evidence gates (Nigel, 2026-11-20, decision 4A).

## What is in it

| File | What it defines |
|---|---|
| [`project.md`](project.md) | The project format: objective, phases, derived objectives, exit gates, workstreams, repos; the owner's gates in a project; the evidence result block |
| [`pipelines/freeform.yaml`](pipelines/freeform.yaml) | Ad hoc work: one stage, no gates. Today's Theseus exactly |
| [`pipelines/feature.yaml`](pipelines/feature.yaml) | research → spec → plan → implement → verify → review → compound |
| [`pipelines/bug.yaml`](pipelines/bug.yaml) | reproduce (failing test first) → rule (only when the fix needs one; the owner's gate, as the bug's spec) → fix → verify, then review and compound when the rule stage ran |
| *(per repo)* | Each repo keeps its own reviewer rubric, drawn from that codebase's dated decisions, and names it in its `AGENTS.md`. The kit has none: a rubric is the repo's, not the method's. Theseus's is `.agents/reviewer-rubric.md` |

## Two modes

- **Standalone (ad hoc).** A card, a session, a handoff. Runs `freeform`. No project file, no artefacts
  folder, no gates, not one more click than Theseus has always needed.
- **Project.** A project file, `project.md` in the project's home folder, sets an objective and phases. Each workstream serves one
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
- **No machine paths or card ids in a public repo's records.** Use placeholders (`<scratch>`, `<worktree>`, "the
  workstream log") or a description. Sub-agent briefs carry this rule, and a check in the suite
  (`PublicFilesTests`) fails any change whose `method/` files contain a
  user-profile path, a session scratch path, a drive path with folders, or a card id. (close-step P19, Nigel
  2026-11-19; every stage 0 record broke it before it existed.)
- **A test of a decision moved out of the UI feeds it the state the caller actually holds,** step by step through the
  sequence it serves, not inputs chosen for convenience. (close-step P22; a test passed because it handed in a request
  the App had already retired.)

- **A child exits once its report is in; its parent restarts it when there is more.** A workstream that reports back
  to the one that proposed it stops once its report is submitted. The parent reads it, and when there is more work it
  restarts the child with `start_workstream` and an instruction ("more work: ..."), or pauses or stops it, through the
  Theseus tools: in a project they reach every card of the project, and a card the owner paused waits for his Allow;
  on the ordinary board only the session's own descendants, cards made from proposals it raised and the owner
  approved. A parent never closes another card's session any other way. (1.1e AC 14 and 17, Nigel 2026-11-21;
  sessions-run-the-project, Nigel 2026-11-22.)

## Working unattended: decide, record, or park

Added for `method-v0.3` from the lessons of the 2026-11-24 Bug Fixing night. Overnight, 73 waits held sessions for
about 68 hours of session time, and only 9 of them were truly the owner's call. Most of the rest came from a session
holding a question that a rule already answered, or that it could have answered itself and recorded.

When a session meets a choice it would otherwise ask about, it goes down this ladder:

1. **Owner-only: never decided by a session.**
   - Completing, approving, abandoning or auto-completing a pull request into the trunk, and pushing or force-pushing
     to the trunk (a guard refuses these).
   - Deleting anything: branches, worktrees, files, records.
   - Creating a tracker item.
   - Accepting a spec or a bug's rule, and confirming a report.
   - Changing a standing rule.
   - Production, secrets, customer data and every Azure mutation.
   - A product decision the brief and the project's decisions leave open.
2. **Decide and record.** Anything else, the session decides: it takes the option it would recommend, provided all of
   these hold:
   - it keeps every standing rule;
   - it can be undone before the change merges, because it lives on the workstream's branch or in its own records;
   - it changes no spec, rule or decision the owner has accepted;
   - the facts it rests on are checked against the real system, not remembered.

   It records the choice with `record_decision`, by "<card> (default, for review)". It lists the choice in REPORT.md
   under **Decided without the owner**, with the option it passed over. The owner can overturn it at review.
3. **Park.** When step 1 applies, or step 2's conditions do not hold:
   - The session writes the question with its options and its recommendation, calls `ask_owner`, and records who it
     waits on with `set_waiting`.
   - Then it carries on with every part of the work that does not depend on the answer.
   - When nothing is left, it commits what it has, writes its handoff, calls `report_back` with verdict `partial`, and
     stops. The owner's answer starts it again.

   A session never holds its slot waiting, and never ends a turn on a question written in prose: an ended turn looks
   finished on the board. The question tool holds the session until it is answered, so use it only when the owner is at
   the desk.

Further rules for unattended runs:

- **Check a fact before it becomes a decision or a brief.** Read the tracker's real workflow states (for example its
  transitions for an item), search for tests whose expectations the fix reverses, and check how live code uses what a
  change blocks. A wrong fact written into a brief became the night's longest single wait, at about 10 hours.
- **Once the report is in, stop.** Write the handoff, call `report_back`, and end the session. The owner confirms a
  report on the board, and that needs no live session. Overnight, finished sessions held their slots for about 12 hours.
- **A shared notes repo is committed with care.**
  - Stage only your own paths, never `git add -A`, and commit with `git commit -- <paths>`.
  - If `index.lock` exists, wait and retry with a growing delay, up to five tries. Then record it on your card and carry
    on.
  - Never delete a lock file: another session's commit may be running.
- **A wait is never a sleep.**
  - A locked test or build run is one command line in its own call, because the lock guard reads only that line.
  - Run anything long in the background and wait for it with a monitor's until-loop, not `sleep`.
  - Meanwhile, do the work that does not depend on it.
- **A failed turn is not the end of the work.** When a turn ends on a network or API error, the next session to look,
  or Theseus, resumes it with "carry on where you were". The handoff says where that is.

## Decisions recorded here

| Date | Decision | By |
|---|---|---|
| 2026-11-18 | The kit lives in the Theseus repo under `method/` for now; it may move to its own repo later | Nigel |
| 2026-11-18 | Work artefacts go in top-level `work/<issue>/`, retrospectives in `bootstrap/`, both outside the `docs/` Pages site (superseded 2026-11-20, below) | Nigel |
| 2026-11-18 | Holdout scenarios live in a separate private repository, which implementing sessions are refused access to | Nigel |
| 2026-11-18 | Projects sit above workstreams; standalone workstreams are unchanged; both may span repos | Nigel |
| 2026-11-19 | The kit stays generic in `method/` on GitHub for now, ships with Theseus as the default, and moves only if other team members start using it. A repo's own rubric and AGENTS.md live in that repo | Nigel |
| 2026-11-20 | Every project's records (its `project.md`, `work/<slug>/` and retrospectives) live in a project home folder outside any code repo, one subfolder of the projects folder per project; a code repo holds code only. `method/starter/` holds the files a new project begins with. Supersedes the 2026-11-18 row on `work/` and `bootstrap/` | Nigel |
| 2026-11-20 | Inside a project the owner's gates are each workstream's spec (a bug's rule) and the primary branch's pull request into the trunk, which passes when he merges it; plan and review pass on evidence (decision 4A, released in `method-v0.2`) | Nigel |
| 2026-11-20 | The evidence result block is JSON in a fenced `theseus-evidence` block; its `gates` field is information only, never proof | Nigel |

## Open questions

- **Setting up the method.** Proposed (not decided): ad hoc work needs no setup; authoring a repo's
  AGENTS.md, a project file or a pipeline is done by an agent started from Theseus, reviewed as a pull
  request, rather than in a form; Theseus's UI is for seeing where work is, its evidence, and approving
  gates and promotions; Theseus reports malformed method files rather than ignoring them.
- **Where the kit lives long-term:** here for now (decided 2026-11-19); revisit when other team members use it.
- **Pinning, by hand for now:** a project names the kit version it follows, as a `Kit:` line in its project file, so
  a change to the kit does not reach a running project unannounced. Versions are git tags `method-v<major>.<minor>`
  in this repo, separate from app releases; the first is `method-v0.1` (2026-11-19). A project moves to a newer
  version deliberately. From `method-v0.2` Theseus reads the `Kit:` line: a project still at `method-v0.1` is shown as
  pinned to it and not read further.
- **L1 Prepared:** [definition not yet written].
