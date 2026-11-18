# Theseus bootstrap

The first project written in the method's format ([`method/project.md`](../method/project.md)). Each
stage is delivered through the method the previous stage made possible.

## Objective

Grow Theseus from a supervisor of interactive Claude Code sessions into a general-purpose, model-neutral
method layer for agentic development, usable from one developer to a team, by using it on itself.

## Acceptance criteria

- [ ] Every phase below has passed its exit gate, on evidence.
- [ ] A team other than the owner has run real work through it (phase 5).

## Repos

| Repo | Role | Autonomy |
|---|---|---|
| github.com/nigelbasel/Theseus | home: code, method kit, artefacts | L0 |
| A private holdout repository | holdout scenarios; never changed by implementing workstreams | owner only |

## Phase 0: Seed the method

**Exit gate:** the owner reviews `bootstrap/stage-0.md` against the criteria below and accepts it.

### Objective 0.1: Method kit v0

- [ ] `AGENTS.md` exists and `CLAUDE.md` points to it, with nothing lost from an earlier `CLAUDE.md`.
- [ ] The reviewer rubric exists, and every item traces to a dated decision in the code.
- [ ] `freeform`, `feature` and `bug` pipelines are defined; `freeform` matches today's ad hoc behaviour
  with no added steps.
- [ ] Pipeline definitions and artefact paths work for a workstream that spans several repos.
- [ ] The project format exists and this file uses it, with stages 0–5 as its phases.

**Pipeline:** freeform
**Workstreams:**
| Card | Branch | Repos | Artefacts |
|---|---|---|---|
| git-22 | main | Theseus | none (this objective's output is `method/`) |

### Objective 0.2: Holdout scenarios

- [ ] Resume tomorrow, stop with handoff and lock contention are written as end-to-end scenarios with
  pass/fail criteria.
- [ ] They are stored where implementing sessions can neither read nor change them.

**Pipeline:** freeform
**Workstreams:** git-22, writing to the holdout repository before its access guard was installed.

### Objective 0.3: Five changes through the method by hand

- [ ] Five small, real Theseus changes, chosen with the owner, mixing feature and bug, each taken through
  its pipeline at L0.
- [ ] Each has its artefacts under `work/<issue>/`.
- [ ] `bootstrap/stage-0.md` records what the method caught, what it missed, the friction (especially for
  ad hoc work), and time and cost.

**Pipeline:** feature or bug, per change
**Workstreams:** all run by card git-22 on `main`, in the Theseus repo, one after another (chosen 2026-11-18):
| Slug | Change | Pipeline |
|---|---|---|
| readme-runtime | The README asks for the .NET 10 runtime; the app targets .NET 11 and ships framework-dependent | bug |
| guard-mentions | The lock guard refuses PowerShell commands that only mention the lock script | bug |
| stop-timing | Record how long a stop takes, from Stop pressed to handoff written and session closed | feature |
| card-branches | A card shows the branch of every repo it covers, not only its working directory's | feature |
| parked-cores | Warn in the status bar when most cores are parked under load | feature |
| resumed-stop | A session resumed under the same id is never closed by a second stop (found in stop-timing's live check; added 2026-11-18) | bug, with review and compound added |

Proposed, not scheduled: **close-step**, a feature covering when the close step runs and which run of a session a stop
belongs to (L1, L2, and the limits left by resumed-stop). See `work/resumed-stop/lessons.md`.

## Phase 1: Evidence

**Exit gate:** every pull request carries evidence, and the holdout scenarios run in CI on Windows.

- Objective 1.1: checks shown on cards. [criteria to be written at the phase 0 exit]
- Objective 1.2: read-only pull request and CI status on cards. [criteria to be written]
- Objective 1.3: a `theseus/evidence` status check. [criteria to be written]

## Phase 2: Unattended

**Exit gate:** 10 pull requests produced at L2 have been merged, with their rework and cost known.

- Objective 2.1: a worktree per repo for each workstream, one branch name across its repos. [criteria to be written]
- Objective 2.2: a headless runner, with budgets and circuit breakers. [criteria to be written]
- Objective 2.3: an overnight queue. [criteria to be written]

## Phase 3: The Hub

**Exit gate:** a second person works through it for two weeks.

- Objective 3.1: shared state and multiple users. [criteria to be written]
- Objective 3.2: GitHub Issues and tracker adapters. [criteria to be written]

## Phase 4: Compound (alongside phases 1–3)

**Exit gate:** 10 lessons accepted into the method kit.

- Objective 4.1: lessons proposed as pull requests to `method/`. [criteria to be written]

## Phase 5: Team pilot

**Exit gate:** [to be written at the phase 3 exit]

## Decisions

| Date | Decision | By |
|---|---|---|
| 2026-11-18 | Projects sit above workstreams; standalone workstreams are unchanged | Nigel |
| 2026-11-18 | A project runs in phases; each phase has derived objectives with acceptance criteria and one exit gate; workstreams serve one derived objective and may run in parallel | Nigel |
| 2026-11-18 | Projects and workstreams may span repos: one branch name, a worktree per repo; autonomy per repo, lowest wins | Nigel |
| 2026-11-18 | Method kit in `method/`; artefacts in `work/`; retrospectives in `bootstrap/`; holdouts in a private repository | Nigel |
