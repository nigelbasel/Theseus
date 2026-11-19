# verifier-card: lessons

Pipeline: `feature` (method kit v0), stage **compound**, autonomy L0 to the plan, then run overnight under Nigel's
autonomy brief (2026-11-19). Branch `nigel/verifier-card`, not pushed. Proposals only: Nigel decides.

## Time and cost (measured)

| Stage | From | To | Notes |
|---|---|---|---|
| research | 17:13:41 | 17:20:25 | A read-only explorer mapped launch, links, reports and MCP (192,726 tokens) |
| spec (5 decisions, one at a time) | 17:20:25 | 17:25:13 | |
| plan | 17:26:47 | 17:26:48 | |
| implement, step 0 (measure AC 0) | 17:29:10 | 17:29:48 | Two headless probes: the hook saw the variable |
| implement | 17:29:48 | 17:40:12 | 1589 of 1589 |
| verify part 1, attempt 1 | 17:40:47 | 18:48:33 | 209,621 tokens. **FAIL** (M1 the guard ignored the working directory; M2). A 51-minute gap with no output in the middle, unexplained |
| implement pass 2 | 18:58:17 | 19:05:06 | Directories swapped (decision 3); 1608 of 1608 |
| verify part 1, attempt 2 | 19:05:33 | 19:21:51 | 221,414 tokens. PASS; about 80 new probes |
| should-fix | 19:23:43 | 19:26:13 | 1610 of 1610 |
| review | 19:26:38 | 19:33:09 | 209,671 tokens. **One must-fix: a verifier could never close** |
| review fixes | 19:35:04 | 19:38:22 | 1617 of 1617 |
| re-verify of review fixes | 19:45:08 | 20:05:55 | 208,940 tokens. PASS; 275 probes |
| should-fix | 20:07:14 | 20:09:48 | 1621 of 1621 |

Sub-agents: 1,042,372 tokens. The holdout scenarios have not run: they are this feature's live check, after Nigel
answers questions 1 and 2, installs the guard and sets the folder.

## What the method caught

- **Measuring the assumption first (AC 0) cost a minute and removed the biggest risk** before any code: a hook does
  see the environment set on the claude process.
- **The first verifier found the guard's real hole (M1):** it judged only the paths a tool named, so a verifier
  sitting inside the holdout could `rm` or `git commit` there. The fix changed the design (decision 3).
- **The review found a defect two verifies passed (M1 of the review):** confirming a verifier's report starts a
  stop, the stop asks for a handoff, and the guard refuses the verifier's write wherever the handoff would go, so it
  could never close. It is where three features meet: the guard, stopping and the close step.
- **Re-verifies caught a spec change I made without marking it** (the own-files refusal changed AC 4): resolved by
  keeping AC 4 as approved.
- **Mutation testing** found untested guards at every round (the real launcher, relative settings, notebooks).

## What it missed, or got wrong

- **Two verifies passed a feature that could not finish its own job.** Each checked the guard and the card in
  isolation; nobody traced the stop that the guard's restriction would interrupt.
- **A spec change slipped in unmarked**, while others (AC 6, AC 10) were marked: the rule existed only in my habits.
- **Verifiers wrote outside their copy folder** three times tonight (once per run), despite the brief. The rule is in
  every brief and still broken.
- **A test of mine failed for the test's own reason** (a margin in a desired size), and **a test host lacked Fluent's
  colours**, so a working trigger looked broken: both caught by reading measured values before changing code.

## Proposed changes to the method kit

| # | Where | Change |
|---|---|---|
| P23 | `method/pipelines/feature.yaml`, plan and verify | **When a change adds a restriction** (a guard, a permission, a refusal), the plan and the verifier trace every flow that acts on the restricted party's behalf through it: its handoff, its stop, its logs, its report. The verifier-card review found what two verifies missed this way |
| P24 | `method/README.md`, rules | **A change that departs from an approved acceptance criterion is marked in the spec, in the same commit**, as awaiting the owner, and listed in the questions file when the owner is away |
| P25 | `method/README.md`, sub-agent briefs | **A verifier's console output goes through its copy folder**, named in the brief as the only place it writes besides its one file; the brief gives the exact `Tee`/redirect target, since a general rule was broken three times in one night |

## Follow-ups (Nigel's decision)

- Questions 1 and 2 (`questions-overnight.md`): the AC 10 directory swap and AC 6's shell rule.
- Installing the guard: copy `tools/holdout/holdout-guard.js` beside the personal guard, point the hook at it, set the
  holdout folder in Tools > Options; retire the personal guard once checked.
- The live check: Verify with holdouts… on close-step's card, running all three scenarios (P18 for pull request #1).
- Recorded limits: a search rooted above the holdout; shell commands; an inherited `THESEUS_ROLE` (C7); two
  verifications in one minute share paths.

## Record

| Stage | Started | Finished |
|---|---|---|
| compound | 20:09:49 (logged) | this commit's time |
