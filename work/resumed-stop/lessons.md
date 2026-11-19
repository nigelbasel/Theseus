# resumed-stop: lessons

Pipeline: `bug` (method kit v0), with a review and this compound stage added by Nigel's decision (2026-11-18), autonomy
L0. Branch `main`, repo Theseus. Proposals only: Nigel decides which, if any, become changes to `method/`.

## Time and cost (measured)

| Stage | From | To | Elapsed | Notes |
|---|---|---|---|---|
| reproduce | 16:39:55 | 16:44:49 | 4.9 min | Includes the refactor commit (16:44:25) |
| rule (+ gate) | 16:48:58 | 16:49:25 | 0.5 min to write | |
| fix | 16:50:08 | 16:55:17 | 5.2 min | |
| verify part 1, attempt 1 | 16:58:13 | 17:02:59 | 4.8 min | Sub-agent, 162,200 tokens. Pass; five gaps |
| fix pass 2 | 17:08:18 | 17:11:30 | 3.2 min | |
| verify part 1, attempt 2 | 17:12:04 | 17:14:47 | 2.7 min | Sub-agent, 133,001 tokens. Pass |
| verify part 2 (live) | 17:32:18 | 19:34:12 | [not recorded] as working time | Spanned a pause while the machine was moved |
| review | 20:21:16 | 20:24:00 | 2.7 min | Sub-agent, 160,394 tokens. Two should-fix |
| review fixes | 20:39:25 | 20:42:34 | 3.2 min | |
| re-verify of review fixes | 20:42:55 | 20:45:47 | 2.9 min | Sub-agent, 138,155 tokens. Pass with notes |

Sub-agents: 593,750 tokens in total. The implementer's own tokens were not measured.

## What the method caught

- **The live check of stop-timing found this defect**, which no unit test could: a resumed session's later stop never
  closed in the same board run.
- **The rule stage settled what the code had never said**: what a second press should do, and when a stop belongs to the
  session running now. It also showed F1 to be worse than "a lost record": a second press could tell a session twice.
- **Every independent pass found something real**: G3 (a regression the rule itself introduced), S1 (one record used for
  two meanings), S2, and then V1 (the S2 fix rarely gets to act). None could type `/exit` twice.
- **Review was worth adding to this bug.** It found the two should-fix items; the bug pipeline as written would have
  ended at verify without them.

## What it missed, or got wrong

- **The implementer said the bug pipeline ended in review and compound.** It does not; Nigel was told, and chose to add
  them.
- **The rule's own rule 4 introduced G3**: "keep the first time" without saying "of this run of the session".
- **Patching kept uncovering the next edge** (G3, S1, S2, V1, V2), each in the same place: the close step's timing and
  its idea of which run a stop belongs to. Three passes in, the right move was to stop and design it, which Nigel chose.
- **The refactor before the failing test** was necessary (the defect sat in an untested view model) but the bug pipeline
  had no place for it.
- **A test file mixed the reproduction test with new tests**, which weakened the "reproduction test unchanged" check. The
  next round put new tests in their own files.

## Proposed changes to the method kit

None is applied yet.

| # | Where | Change |
|---|---|---|
| P7 | `method/pipelines/bug.yaml` | A bug that needed a `rule` stage also gets `review` and `compound`, as the feature pipeline has: a rule means new behaviour, which is what review is for |
| P8 | `method/pipelines/bug.yaml`, reproduce | When the defect is in code no test can reach, a refactor that changes no behaviour may come first, as its own commit, with characterisation tests; the reviewer checks it separately |
| P9 | `method/pipelines/bug.yaml` and `feature.yaml` | New tests go in a new file, never beside a reproduction test, so "unchanged since it failed" stays a plain diff check |
| P10 | `method/README.md`, rules | When fix passes keep uncovering the next edge in the same place (a third pass is the signal), stop: record what is known and propose a feature with a spec instead |

## Follow-ups (Nigel's decision)

- **A "close step" feature**, covering L1 (closes wait for an unrelated refresh), L2 (no sign on the card that a stop is
  under way), the close step's half of S2, V1 and V2. Its research has a head start in `work/stop-timing/` and here.
- Clean up the two scratch cards, their briefs and handoffs.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| compound | 2026-11-18 20:50:33 (logged) | this commit's time | |
