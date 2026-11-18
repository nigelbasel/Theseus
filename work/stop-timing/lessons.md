# stop-timing: lessons

Pipeline: `feature` (method kit v0), stage **compound**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Proposals only: Nigel decides which, if any, become changes to `method/`.

## Time and cost (measured)

| Stage | From | To | Elapsed | Notes |
|---|---|---|---|---|
| research | 13:53:09 | 13:54:38 | 1.5 min | Mined the local Theseus logs for real stop data |
| spec (+ gate) | 13:56:17 | 13:56:46 | 0.5 min to write | Waiting for approval not counted |
| plan (+ gate) | 13:59:53 | 14:00:25 | 0.5 min to write | |
| implement | 14:03:32 | 14:08:32 | 5.0 min | Tests failed first; one compile error |
| verify part 1, attempt 1 | 14:14:34 | 14:19:18 | 4.7 min | Sub-agent, 156,570 tokens. Passed with 3 gaps |
| implement, pass 2 | 14:22:18 | 14:25:47 | 3.5 min | |
| verify part 1, attempt 2 | 14:26:27 | 14:29:51 | 3.4 min | Sub-agent, 136,417 tokens. Passed |
| verify part 2 (live) | about 15:10 | 15:39:20 | about 30 min | Nigel's time on the board, including a release and update; start approximate (the release check) |
| review | 15:39:20 | 15:42:41 | 3.4 min | Sub-agent, 146,196 tokens. No must-fix |
| review fixes | 15:44:54 | 15:50:32 | 5.6 min | |

Sub-agents: 439,183 tokens in total. The implementer's own tokens were not measured. From research to accepted
review: about 2 hours of wall clock, of which the agent's own work was about 30 minutes; the rest was gates, the
release and the live check.

## What the method caught

- **Research changed the feature.** The ask was "time the stop". The local logs showed 5 of 11 stops never finished,
  so the spec added "say how far it got". Without a research stage this would have shipped as timing only.
- **The independent verifier caught the implementer's silent drift from the plan** (F2 of part 1: the plan said
  `AtomicFile`, the code did not use it, and the implementation note did not say so), and a false log line (F1).
- **The live check found a real, older defect** (L3: a resumed session is never closed by a second stop) that no
  unit test could, and gave figures for the first time (close delays of 87 s against a 20 s quiet period).
- **The rubric's first real use worked**: the reviewer checked all 19 items, none broke, and its should-fix findings
  were concrete.

## What it missed, or got wrong

- **The implementer's instructions for the live check were wrong twice**: "press Enter" (Theseus nudges idle sessions
  itself) and "press Focus to refresh" (Focus does not refresh). Both were written from memory, not from the code.
  The "stays idle" card could never show an unfinished stop for the same reason.
- **The implementer stated a cause before checking it** ("Focus re-read the sessions"; "nothing re-checks after the
  quiet period"), then found the real cause (L3) by reading the code. The evidence records both corrections.
- **A sub-agent deleted its own temp folders without asking**, because the brief did not carry the owner's
  no-delete rule. Sub-agents do not inherit the rules the implementer holds.
- **The plan's promise of "queryable" log values** was quietly dropped in pass 2; it is now recorded, but the plan
  should not have promised a property it had not checked the logger supports.
- **A newly approved card landed where the current board view hid it** (folded "Not running" column). Theseus
  friction, not a method gap; noted for the bootstrap retrospective.

## Proposed changes to the method kit

Each is small and independent. None is applied yet.

| # | Where | Change |
|---|---|---|
| P1 | `method/README.md`, rules | Stage times come from a log line written when the stage starts, or a commit's time. An agent never writes an estimated time; a time not captured is "[not recorded]". (Seen in readme-runtime and here.) |
| P2 | `method/README.md`, rules | A brief for a sub-agent (verifier, reviewer) carries the owner's standing rules it needs: no deletes of any kind, read-only scope, no tracker writes, no pushes. Sub-agents do not inherit them. |
| P3 | `method/pipelines/feature.yaml`, implement | `implementation.md` lists **every** difference from `plan.md`, however small, including anything the plan promised and the code does not do. |
| P4 | `method/pipelines/feature.yaml` and `bug.yaml`, verify | Steps a person runs for verification are checked against the code before they are given, and say what each step should produce. |
| P5 | `method/pipelines/bug.yaml` | When a fix needs a rule the code does not already state (a parser, a policy), write the rule as a short spec, approved by the owner, before the fix. Each guard-mentions pass fixed one class of input and opened another until the rule ("fail closed when unsure") was written down. |
| P6 | `method/pipelines/bug.yaml`, reproduce | The failing test is committed on its own; do not push between reproduce and fix on a branch that CI builds and releases from. |

## Follow-ups (Nigel's decision)

- **L3** as its own bug change, taking F1, F3 and F4 from the review.
- L1 (close delays after the quiet period) and L2 (no sign on the card that a stop is under way) as candidates.
- The holdout scenario's `[stop-deadline]`: wait for a few weeks of these log lines first.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| compound | 2026-11-18 16:18:39 (logged) | this commit's time | |
