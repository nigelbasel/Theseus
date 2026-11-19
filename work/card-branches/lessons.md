# card-branches: lessons

Pipeline: `feature` (method kit v0), stage **compound**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Proposals only: Nigel decides which, if any, become changes to `method/`.

## Time and cost (measured)

| Stage | From | To | Elapsed | Notes |
|---|---|---|---|---|
| research | 11-18 20:52:44 | 20:53:56 | 1.2 min | Read the code and surveyed the owner's 87 cards, read only |
| spec (+ gate) | 20:58:23 | 20:58:43 | 0.3 min to write | One decision asked (where repos are declared) |
| plan (+ gate) | 20:59:42 | 21:00:06 | 0.4 min to write | |
| implement | 21:06:36 | 21:13:21 | 6.8 min | One slip corrected before commit |
| verify part 1, attempt 1 | 22:06:11 | 22:11:43 | 5.5 min | Sub-agent, 160,521 tokens. **Failed**: F1 |
| implement pass 2 | 11-19 06:40:50 | 06:46:23 | 5.6 min | |
| verify part 1, attempt 2 | 06:46:48 | 06:49:43 | 2.9 min | Sub-agent, 155,874 tokens. Pass |
| verify part 2 (live) | about 08:48 | 08:53:18 | about 5 min | Nigel on the board; start approximate (the board restart) |
| review | 08:53:26 | 08:56:37 | 3.2 min | Sub-agent, 155,085 tokens. Two should-fix |
| review fixes | 09:01:43 | 09:04:28 | 2.8 min | |

Sub-agents: 471,480 tokens in total. The implementer's own tokens were not measured. The implementer's own working time
was about 25 minutes; the rest was gates, a release, an overnight break and the live check.

## What the method caught

- **Research turned the feature round.** The ask was "show every repo's branch". The survey showed the card did not
  know its repos at all (85 of 87 cards started in the notes repo; 3 declared repos; none spanned two), so the spec gave
  cards their repos instead of only reading more branches.
- **The independent verifier caught a defect that every test passed:** WPF ignores `StringFormat` on a `ToolTip`, so the
  "identical to today" tooltip the implementer copied from the XAML had never been shown. The verifier proved it with a
  WPF probe. The implementer's test had asserted the XAML's text, not what a user saw.
- **The live check confirmed the defect fixed** in the place it matters: hovering a real card.
- **Review found two real gaps** the verifiers had not: a fall-back to a guessed link, and a stale tooltip.
- **P10 (stop after the third pass) was used for the first time** and kept the remaining edge cases as recorded limits.

## What it missed, or got wrong

- **"Identical to today" was tested against the source, not the rendering.** For UI behaviour, the source is not the
  truth: a binding can be written one way and rendered another.
- **A wrong expectation in the implementer's own test had to be changed in a later pass.** It was a correction, judged so
  by the re-verifier, but the method has no word for it and the "tests unchanged" checks only cover reproduction tests.
- **The live check could not reach PR links**: the scratch repos had no remotes. Both PR gaps were then found by review,
  not by a person.
- **A sub-agent's mis-passed argument created a stray folder** (`--nologo`); harmless, removed with Nigel's leave.

## Proposed changes to the method kit

None is applied yet.

| # | Where | Change |
|---|---|---|
| P11 | `method/pipelines/feature.yaml`, verify | For UI behaviour, "unchanged" is checked against what is rendered (a running board or a rendering probe), not against the source markup |
| P12 | `method/README.md`, rules | An existing test may be changed only to correct a wrong expectation, in its own line of `implementation.md` saying what it asserted and why that was wrong, and the verifier judges whether it is a correction or a weakening |
| P13 | `method/pipelines/feature.yaml`, verify | A live check's scratch setup is built to exercise every acceptance criterion it claims; criteria it cannot reach are named in the plan, so review knows where to look |

## Follow-ups (Nigel's decision)

- The recorded limits (implementation.md, end): a junction above a repo; Azure DevOps and port forms of the same origin;
  case-sensitive git servers; `\\?\` paths; App wiring tests.
- The scratch feature folder (`scratchpad\live-card-branches\`) can be removed; the scratch card's extra directory should
  be taken off.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| compound | 2026-11-19 09:06:21 (logged) | this commit's time | |
