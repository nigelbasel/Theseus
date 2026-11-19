# close-step: lessons

Pipeline: `feature` (method kit v0, P1 to P18), stage **compound**, autonomy L0. Branch `nigel/close-step`, pull request
#1. The first change of phase 1, and the first through a pull request. Proposals only: Nigel decides which, if any,
become changes to `method/`.

## Time and cost (measured)

| Stage | From | To | Elapsed | Notes |
|---|---|---|---|---|
| research | 12:58:53 | 13:01:25 | 2.5 min | Found L1's cause: there was no timer |
| spec (+ gate) | 13:01:25 | 13:02:51 | 1.4 min to write | Nigel chose to build before the verifier card, with verify held open (P18) |
| plan (+ gate) | 13:03:38 | 13:04:27 | under 1 min to write | |
| implement | 13:08:28 | 13:18:24 | 9.9 min | Tests first (924e0e4); 1587 of 1587 |
| verify part 1, attempt 1 | 13:19:41 | 13:42:39 | 23.0 min | Sub-agent, 193,767 tokens. **Failed:** F1 |
| implement pass 2 | 14:33:45 | 14:41:09 | 7.4 min | Tests first (bb9b753); 1614 of 1614 |
| verify part 1, attempt 2 | 14:41:42 | 15:06:42 | 25.0 min | Sub-agent, 198,016 tokens. Pass |
| verify part 2 (live) | 16:17:29 | about 16:31 | about 14 min | On the pull request's CI build, installed by Nigel |
| review | 16:31:44 | 16:37:11 | 5.5 min | Sub-agent, 198,385 tokens. No must-fix; S1 to S4 |
| review fixes | 16:39:31 | 16:42:29 | 3.0 min | |
| re-verify of review fixes | 16:42:37 | 16:44:55 | 2.3 min | Sub-agent, 131,820 tokens. Pass; one should-fix (an incomplete scrub) |
| S2 (Close now in the menu) | 16:47:30 | 16:49:49 | 2.3 min | Test first; 1615 of 1615 |

Sub-agents: 721,988 tokens. The implementer's own tokens and Nigel's time at gates were not measured (phase 1
objective 1.5 adds both). The holdout `stop-with-handoff` has not run, so verify is still open and the pull request
unmerged.

## What the method caught

- **Research found the real cause of L1** in minutes: the close step had no timer, and the refresh a session's own
  change caused always came before its quiet period ended. Live: closes went from 87 s to 11 minutes down to 21.3 s
  after quiet.
- **The verifier caught a real defect (F1)** that every test passed: once `/exit` was sent the request was retired,
  so the card lost its stop and offered Stop again while the session was closing.
- **Mutation testing (P16) showed the App's guards were untested**, which led to moving the card's stop view, the
  Close now rule and both notices into Core, where they are now tested.
- **The reviewer caught a safety gap (S1)** that verification passed: the timer acted on a session snapshot up to
  about 20 s old, so `/exit` could in principle be typed into a session that had just started a turn.
- **Reading the code before the spec (P4, P15) changed the design of V2:** every attempt raises the terminal, so
  nothing retries a failed `/exit` on its own.
- **The pull request flow worked:** CI built the branch's MSI, Nigel installed it, and the live check ran on exactly
  what will merge, with nothing reaching the update channel.

## What it missed, or got wrong

- **A Core test passed only because it handed the function a state the App no longer held** (F1): the request after
  `/exit` was sent. Moving a decision out of the App made it testable, but the test chose its own convenient inputs.
- **Moving the close step out of the refresh lost something the refresh had provided** (S1): fresh session data. The
  plan listed risks of the new path but not what the old path had guaranteed.
- **The research was partly wrong about L2:** a status line already showed while a stop was waiting. It was stated
  without checking what the card rendered.
- **A live-check step was given to Nigel from memory** ("a banner"), when the notice is in the status bar. P4 already
  requires steps to be checked against the code; the verifier's checked list existed, and I paraphrased it instead.
- **Machine paths and the card id went into public files**, here and in every stage 0 change, and a hand scrub of
  this change's files was incomplete until a re-verify found the rest.
- **A test of mine combined two states that cannot occur together**, and failed for that reason.

## Proposed changes to the method kit

None is applied yet. `method/` is changed through a pull request from phase 1.

| # | Where | Change |
|---|---|---|
| P19 | `method/README.md`, rules; the evidence check | **No machine paths or card ids in anything committed to a public repo:** placeholders (`<scratch>`, `<worktree>`, "the workstream log") instead. Sub-agent briefs carry the rule, and a CI check fails a change whose `work/`, `bootstrap/` or `projects/` files contain a drive or user-profile path (Nigel's decision 3, 2026-11-19) |
| P20 | `method/pipelines/feature.yaml` and `bug.yaml`, verify | **Live-check steps are given to the owner exactly as the verifier wrote and checked them**, not paraphrased. A step that has to change is checked against the code again first |
| P21 | `method/pipelines/feature.yaml`, plan | **When code moves out of an existing sequence** (a refresh, a pipeline, a lock), the plan lists what that sequence guaranteed (fresh reads, order, locks held) and how the new path keeps each |
| P22 | `method/README.md`, rules | **A test of a decision moved out of the UI feeds it the state the caller actually holds at that point**, step by step through the sequence (for example: before `/exit`, after it is sent, after the request is retired), not inputs chosen for convenience |

## Follow-ups (Nigel's decision)

- **Simplify `ExitsTyped`** (review S3): `_finished` can be derived from the last attempt, and `TryCloseNow` puts the
  Close now press's logic in a record of what was typed.
- **The public-paths cleanup** (decision 3, option A): a separate pull request after close-step. Branch name to be
  proposed (`nigel/public-paths`).
- **The verifier card ends itself** once its report is confirmed (raised with Nigel, 2026-11-19): for objective 1.0's
  spec.
- **The parked-cores blink**, next after close-step, as planned.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| compound | 16:53:53 (logged) | this commit's time | |
