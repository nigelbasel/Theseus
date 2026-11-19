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

**close-step** is scheduled as the first pull request of phase 1, before the verifier card (Nigel, 2026-11-19). It is a
feature covering when the close step runs (L1: closes wait for an unrelated refresh), showing that a stop is under way
(L2), and which run of a session a stop belongs to (resumed-stop V1, V2). See `work/resumed-stop/lessons.md`. The
parked-cores blink (`work/parked-cores/review.md`, finding 4) follows it as a separate small change.

## Phase 1: Evidence

**Exit gate:** every pull request carries evidence, and the holdout scenarios run in CI on Windows.

- **Objective 1.0 (first): a verifier card.** When a workstream reaches `verify` and its spec names a holdout scenario,
  Theseus itself launches a fresh verifier session that can read the holdout repository and cannot change the code.
  The verifier runs the scenario, asks the owner in its own tab for any step only a person can do, and hands back the
  scenario name, pass or fail and a one-line reason through `report_back`, confirmed on the board. The implementing
  session sees only that verdict. The owner never relays results between sessions. Needs a verifier card type
  (Theseus starts it with holdout access, which a card cannot have today) and a read-only mode in the holdout guard.
  Its first real test is running all three holdout scenarios against the stage 0 changes. (Nigel, 2026-11-19: the
  scenarios were written in stage 0 and never run, because no verifier could run them.)

**Criteria approved by Nigel, 2026-11-19.** Expect 1.4 to finish last: running scenarios with nobody at the board
needs a way to drive Theseus unattended, and Windows minutes in a private repo are paid for.

### Objective 1.0: A verifier card
- [ ] From a project workstream at `verify`, the owner starts a verifier card with one action on the board. Nothing
      starts it on its own.
- [ ] The verifier session starts fresh: no resumed session, nothing from the implementer's conversation.
- [ ] The verifier can read the holdout repository and cannot change it or any code repository. A write attempt is
      refused, and a test proves it. An implementing session is still refused any access, and the existing guard
      tests still pass.
- [ ] The verifier hands back each scenario's name, pass or fail and a one-line reason through `report_back`. The
      owner confirms it on the board, and the implementing session sees only that verdict through `related`.
- [ ] All three holdout scenarios are run this way against the stage 0 changes, with results recorded against
      criterion 6 of `bootstrap/stage-0.md`. Each failure becomes a bug through the method.

### Objective 1.1: Checks shown on cards
- [ ] The kit defines a machine-readable result block in `evidence.md` (stage, result per repo, scenarios run),
      released as `method-v0.2`.
- [ ] A project workstream's card shows its current stage and whether that stage's gate has passed, read from
      `work/<slug>/`. A stage with no evidence shows as "no evidence", never as passed.
- [ ] An ad hoc card is unchanged: no new element and no new click. Checked against what is rendered (P11).

### Objective 1.2: Read-only pull request and CI status on cards
- [ ] A card shows, for each repo it covers, its pull request (if any) and the latest CI run for its branch. Theseus
      only reads.
- [ ] It works with no token stored on the machine, reading GitHub only, within GitHub's limit for calls without a
      token (conditional requests, so an unchanged status costs nothing). A repo on any other host shows its status
      as "unknown: <host> not read yet". Azure DevOps comes with the adapters in phase 3, when its authentication is
      designed (Nigel, 2026-11-19: GitHub only in phase 1; a session's report is never shown as CI status).
- [ ] When the status cannot be read it shows as unknown, with the reason in the tooltip, never as passing.

### Objective 1.3: The evidence check
- [ ] A CI job in each project repo fails when a change's `work/<slug>/evidence.md` is missing, malformed or not
      passing. It runs in that repo's own CI, so Theseus writes nothing to GitHub and needs no token. It replaces
      the `theseus/evidence` status first planned (Nigel, 2026-11-19), and branch protection makes it required.
- [ ] The owner's acceptance of each gate (who and when) is recorded in the evidence file, and the job checks it.
- [ ] The job is in the kit, so any project repo can adopt it.

### Objective 1.4: Holdout scenarios in CI on Windows
- [ ] The scenarios that need no person run in the holdout repository's own GitHub Actions, on Windows, against
      each new Theseus release. Their steps and logs therefore stay private, and nothing about them reaches the
      public repo's CI.
- [ ] A failure is visible on the board for the release it tested.
- [ ] Scenarios that need a person stay with the verifier card.

### Objective 1.5: Carried over from stage 0
- [ ] One change spans two repos, on one branch name with a worktree per repo, through its pipeline.
- [ ] Each stage records the implementer's own tokens and the owner's time at its gate, measured and not estimated.

**Exit gate:** every change made in phase 1 carries evidence that the evidence check passed. The holdout
scenarios that need no person run in CI on Windows for every release. The verifier card has run all three scenarios.

**Changes go through pull requests from phase 1** (Nigel, 2026-11-19). Each change goes on a short-lived branch, whose
name the owner approves before it is created, and opens a pull request. CI and the evidence check gate the merge. The
owner merges every pull request himself; no session does. Stage 0's commits straight to `main` end here.

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
| 2026-11-19 | The lock guard's older bypasses (`&`, `$(...)`, backticks, a script path that is not the real one) are accepted limits under the guard's rule 0; revisit only if one appears in a real transcript (`work/guard-mentions/fix.md`) | Nigel |
