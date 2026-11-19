# parked-cores: spec

Pipeline: `feature` (method kit v0), stage **spec**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Scope: option A of `research.md`, chosen by Nigel on 2026-11-19.

## Goal

When the machine is holding most of its cores parked while the cores it has left are saturated (the "clamp" that cost
a day in November 2026), the status bar says so, in amber, with a likely cause. At idle and in healthy heavy use it
says nothing.

## Acceptance criteria

**Reading**

1. Each resource sample (every 5 seconds, as today, off the UI thread) also reads, for every logical processor,
   whether it is parked and how busy it is, from the Windows performance counters, without admin.
2. If the counters cannot be read (not available, access refused, a machine that does not park), there is no warning
   and nothing else changes. The failure is logged once, not on every sample.

**The rule**

3. A sample **shows a clamp** when both hold:
   - the share of logical processors parked is at least **[parked share]**, and
   - the processors not parked are, on average, at least **[running busy]** busy.
4. The warning **starts** after **[held samples]** clamp samples in a row, and **stops** at the first sample that does
   not show one.
5. The three figures are settings with these provisional defaults, chosen from one machine's recorded samples
   (`research.md`) and to be revisited with more data:

   | Setting | Default | Why this default |
   |---|---|---|
   | [parked share] | 50% | The recorded clamps had 80% parked; healthy heavy use never exceeded 35% (7 of 20) while busy. Half sits between with room either side |
   | [running busy] | 90% | In the clamps the few running cores carried the whole load; at idle (also 80% parked) they were nearly idle, which this separates |
   | [held samples] | 3 (15 seconds) | Long enough that a burst does not flash it, short enough to see while the slowness is being felt |

   They are read from the board's settings file and are not shown in the settings window.

**What is shown**

6. While the warning stands, the status bar shows, after the machine line and in the board's amber, for example:
   "Cores held back: 16 of 20 parked under load". Otherwise nothing extra is shown and the machine line is unchanged.
7. Its tooltip says what it means and what to check, in a few lines: the machine is keeping most cores parked while the
   rest are saturated; the usual cause is heat (vents, fans, a soft surface under a laptop), sometimes the power plan's
   minimum cores setting; the figures behind it.
8. When the warning starts and when it stops, the Theseus log gets one line each, with the figures (parked count of
   total, running cores' average busy, the clock against base), so the defaults can be checked against real events.

**Unchanged**

9. Everything else about the status bar and the resource sample, and the board's behaviour on machines where nothing
   parks.

## Out of scope

- A setting in the settings window for the three figures.
- Acting on a clamp (changing a power plan, notifying a session).
- Telling heat from a power plan apart.
- Recording a history of samples.

## Holdout scenarios that apply

None.

## How it will be verified

- Unit tests in `Theseus.Core` for: the rule on recorded figures (a clamp, idle with many parked, healthy heavy use,
  a burst shorter than the hold, a warning that stops); shares on other core counts; the status text and tooltip; and
  the "cannot read" path.
- The full suite.
- On a running board: the warning stays silent at idle and under a heavy but healthy load. **The firing path cannot be
  produced on demand** on a healthy machine (the clamp came from heat), so it rests on the unit tests with recorded
  figures; the plan names this (method rule P13).

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| spec | 2026-11-19 09:11:44 (logged) | this commit's time | Gate: Nigel approves this spec, including the three provisional defaults |
