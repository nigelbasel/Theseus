# parked-cores: research

Pipeline: `feature` (method kit v0), stage **research**, autonomy L0. Branch `main`, repo Theseus.
No code changes in this stage.

## The ask

Warn in the status bar when most cores are parked under load. In November 2026 an owner's laptop slowed badly; finding
out why took most of a day, and the cause turned out to be the machine holding most of its cores parked while under
full load, because it was too hot (dusty fans; fixed by cleaning). A warning on the board would have pointed there at
once.

## What exists today

- The status bar's machine line, `ResourceText.Machine`: "CPU 48% · sessions 30% · Defender 6% · Docker 12%", from a
  sample every 5 seconds (`ResourcesViewModel`, off the UI thread), built on `NtQuerySystemInformation` and
  `GetSystemTimes`. No admin needed.
- Nothing reads core parking or the processor's clock today.

## Can the board read it? (measured, 2026-11-19, unelevated, 20 logical processors)

| Way | Result |
|---|---|
| `\Processor Information(*)\Parking Status` through PowerShell's `Get-Counter` | Works without admin; 4.3 s per call, mostly PowerShell's own overhead and its 1 s sample |
| The same counter through the PDH API directly (`PdhOpenQuery`, `PdhAddEnglishCounter`, `PdhCollectQueryData`, `PdhGetFormattedCounterArray`), as the board would | Works without admin; about 0.4 s to open the query once, **under 1 ms per read** after that; matched `Get-Counter` (6 of 20 parked at that moment) |

The same counter set has `% Processor Time` per core and `% Processor Performance` (clock against base), so one query
gives all three. No package is needed (`pdh.dll` is part of Windows), in keeping with the board's other native calls.

## What a clamp looks like, against normal use (recorded during the investigation; one machine)

| Situation | Samples | Cores parked (of 20) | Total busy | Clock (of base) |
|---|---|---|---|---|
| Idle, healthy | several | **16 to 17** | 4 to 15% | 91 to 114% |
| Clamped under full load (the problem) | 14 | 16 | **27 to 59%** (median 29%) | 91 to 181% |
| Normal heavy use after the fix | 213 | **at most 7** whenever total busy was 50% or more | 50%+ | |
| A moderate moment today | 1 | 10, then 6 | 45% | 151% |

What this means:

- **Many parked cores is normal.** Windows parks most cores when there is little to do: 16 of 20 at idle.
- **Total busy is a poor signal.** When most cores are parked, the total cannot rise far, so the clamp showed a median of
  29%. A rule like "12 or more parked and 50% busy" would miss most clamps (the investigation's own watcher used that
  rule).
- **What sets a clamp apart is the cores left running being saturated while most are parked**: work is waiting, and the
  machine will not wake the cores it has parked. At idle the running cores are not busy; in healthy heavy use few cores
  are parked.

## Options

| Option | What it is | For | Against |
|---|---|---|---|
| **A. Warn on "parked and the rest saturated", held for a while** (recommended) | Read parking and per-core busy each sample; warn when at least half the cores are parked **and** the cores not parked are nearly all busy, for several samples in a row. Status bar shows it in amber, for example "Cores held back: 16 of 20 parked under load", with a tooltip saying the usual cause (heat, or a power plan) and what to check | Fits the evidence: silent at idle and in healthy heavy use, fires on the clamp. Held over several samples so a moment does not flash it | Its figures (how many parked, how busy, for how long) come from one machine's 14 samples, so they are a starting point, not a measurement |
| B. Warn on "many parked and total busy above X" | Simpler | | The evidence above says it misses most clamps |
| C. Always show the parked count | "16/20 parked" in the status bar, no warning | Shows everything, decides nothing | Normal idle would read 16/20, which looks like a problem and is not; it trains people to ignore it |

## Recommendation

**A.** Three figures need deciding in the spec, and none can be measured better than this today: the share of cores
parked (half is the evidence's floor), how busy the running cores must be, and for how many samples. The spec should
name them as settings with defaults taken from this data, and say they are provisional.

## Constraints found

- The status bar is for readings (rubric R16); a warning there must be text in the board's amber, not a dialog.
- The board is used on other machines with other core counts; the rule has to be in shares, not counts.
- On a machine where parking is disabled (some power plans, desktops), nothing is ever parked: the warning simply never
  shows. Not an error.
- The repo is public: no machine model, service tag or local path in code, tests or docs.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| research | 2026-11-19 09:07:54 (logged) | this commit's time | Read the resources code; measured the counter unelevated, through Get-Counter and through PDH; read the investigation's recorded samples |
