# parked-cores: lessons

Pipeline: `feature` (method kit v0), stage **compound**, autonomy L0. Branch `main`, repo Theseus.
Proposals only: Nigel decides which, if any, become changes to `method/`.

## Time and cost (measured)

| Stage | From | To | Elapsed | Notes |
|---|---|---|---|---|
| research | 09:07:54 | 09:09:47 | 1.9 min | Measured the counter unelevated two ways; read the investigation's recorded samples |
| spec (+ gate) | 09:11:44 | 09:11:46 | under 1 min to write | Three provisional defaults approved |
| plan (+ gate) | 09:18:33 | 09:18:53 | under 1 min to write | Named the part verification cannot reach (rule P13) |
| implement | 09:27:44 | 09:34:02 | 6.3 min | |
| verify part 1, attempt 1 | 09:57:55 | 10:04:57 | 7.0 min | Sub-agent, 146,931 tokens. **Failed:** wrong power setting in the tooltip |
| implement pass 2 | 10:31:05 | 10:34:14 | 3.2 min | |
| verify part 1, attempt 2 | 10:34:24 | 10:41:08 | 6.7 min | Sub-agent, 149,126 tokens. Pass, but found a regression pass 2 made (N1) |
| implement pass 3 | 10:44:17 | 10:49:09 | 4.9 min | |
| verify part 1, attempt 3 | 10:49:36 | 10:55:21 | 5.8 min | Sub-agent, 133,841 tokens. Pass |
| verify part 2 (live) | 11:21:40 | 11:32:17 | about 11 min | Two one-minute full loads; Nigel watched the second |
| review | 11:33:41 | 11:36:34 | 2.9 min | Sub-agent, 153,490 tokens. Acceptable as is |
| review tidy | 11:40:49 | 11:43:46 | 3.0 min | Tests only |

Sub-agents: 583,388 tokens in total. The implementer's own tokens were not measured; its own working time was about
25 minutes.

## What the method caught

- **Research stopped the obvious rule.** "Many parked and busy above 50%", the rule the investigation's own watcher used,
  would have missed most clamps: parking is normal at idle, and total busy stays low in a clamp. The data said: most
  parked while the rest are saturated.
- **The plan said up front what verification could not reach** (the warning firing), so nobody was surprised and review
  knew to look at the recorded-figures tests (rule P13, its first use).
- **The verifier caught a wrong instruction to the user**: the tooltip named a different Windows setting from the one the
  spec meant. Every test passed; the implementer's own test asserted the wrong words.
- **The second verifier caught a regression the first fix introduced**: treating every PDH status as fatal turned the
  warning off for good after a transient status seen in 4 of 300 reads. The implementer had flagged that risk and asked
  for it to be checked; the verifier measured it.
- **Mutation testing by the verifier** showed that "guard tests" passed with the guards removed, which led to tests that
  bite.

## What it missed, or got wrong

- **A test asserted the implementer's own wrong words** (the power setting), again: the same failure as card-branches'
  tooltip. A test written from the code's text checks the code against itself.
- **"Stop after a failure" was a guess about PDH's behaviour**, not a measurement. Research measured the happy path only.
- **A slip against tests-first:** the status classifier and its test were written in the same step.
- **Outside this change, the implementer damaged a file:** a PowerShell read-and-rewrite of the owner's memory index
  (UTF-8 without a BOM, read as ANSI by Windows PowerShell 5.1) double-encoded 188 characters. Repaired exactly and
  checked; every other file written that day was scanned clean.
- **Test files named after fix passes** had to be folded back by review.

## Proposed changes to the method kit

None is applied yet.

| # | Where | Change |
|---|---|---|
| P14 | `method/README.md`, rules | Never rewrite a text file by reading it with Windows PowerShell's `Get-Content` and writing it back: version 5.1 reads UTF-8 without a BOM as ANSI. Use the editor tool, or .NET with an explicit UTF-8 encoding. Sub-agent briefs carry this rule |
| P15 | `method/pipelines/feature.yaml`, research | When the feature reads an external system (an API, counters, a file format), research measures its failure modes as well as its happy path: what errors come back, how often, and whether they pass |
| P16 | `method/pipelines/feature.yaml` and `bug.yaml`, verify | The verifier tries removing each guard or condition the change adds (mutation), and a test that still passes is reported. Three of the four features needed this to find tests that could not fail |
| P17 | `method/README.md`, rules | Text a user acts on (an instruction, a setting's name, a cause to check) is checked against its source (the spec, the vendor's documentation), not against the code that prints it: twice this day a test asserted the implementer's own wrong words |

## Follow-ups (Nigel's decision)

- **The blink** (review finding 4): a skipped sample hides a standing warning for about 15 s and logs a stop and restart.
  Fix by treating a skipped sample as no reading; it changes AC 4. With it, inject `ICoreCounters` and test `ShowClamp`.
- The three provisional figures: revisit once the start and stop log lines have recorded real events.
- Estimating effort from Theseus's recorded times (Nigel's idea, 2026-11-19): a candidate for after stage 1.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| compound | 2026-11-19 11:45:03 (logged) | this commit's time | |
