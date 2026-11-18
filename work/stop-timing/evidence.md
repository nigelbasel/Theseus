# stop-timing: evidence

## Part 2: live check on a running board (2026-11-18)

Board and hook at v2026.11.18.26389 (built from e414465), confirmed from the running files. Two scratch cards, made
from proposals Nigel approved: "Stop check: hands over" and "Stop check: stays idle". Nigel pressed the buttons and
typed in the tabs; the implementer read the Theseus log (`%LOCALAPPDATA%\Theseus\logs\theseus-20261118.log`) and the
stop-request files. Every time below is from the log or a file time.

| Run | What happened | Log line, verbatim |
|---|---|---|
| Hands over | Stop 15:17:12.5; Theseus nudged the idle session itself; `/exit` typed 15:18:43.8; the tab closed by itself | `Stop of stop-check-hands-over finished in 91.3s: delivered after 0.5s, handoff 3.5s later, /exit 87.3s after that` |
| Stays idle, 1st | Stop 15:19:15.4, nudged and delivered, so it handed over rather than staying undelivered. The card still showed Stop; Nigel pressed Stop again at 15:19:52.4 and `/exit` was typed 0.7 s later | `Stop of stop-check-stays-idle finished in 37.7s: delivered after 0.6s, handoff 9.5s later, /exit 27.7s after that` |
| Stays idle, 2nd (resumed, same session id) | Stop 15:23:06.7, delivered 15:23:07.2 (the stamp is on disk: `"deliveredUtc":"2026-11-18T15:23:07.16..."`), handoff 15:23:18.3. Never closed: no log line at all from 15:23:18 to 15:34:20, through Focus, F5 and the Refresh button. Nigel typed `/exit` himself | none (correct: the stop did not finish, and its request is still on disk) |

### Against the spec

- **AC 1, 2, 3, 7, 8 seen working live:** the installed hook stamped the delivery time; each finished stop logged one
  line with all four figures, matching the surrounding log timestamps; stopping behaved as before.
- **AC 5, 6 (unfinished stops):** not yet seen live. The "stays idle" card could not stay undelivered, because Theseus
  nudges an idle session (which the implementer did not foresee). The second run's request is still on disk, so the
  next start of that card will be reported as unfinished ("started again ... handed over, but /exit was never
  typed"); otherwise that path rests on part 1's tests.

### Defects found (all predate stop-timing; none is caused by it)

- **L3 (defect, confirmed): a session resumed under the same id is never closed by a second stop.** `ShellViewModel`
  keeps `_closed`, the sessions it has typed `/exit` into, in memory, and nothing removes an entry. A resumed session
  keeps its id, so after one closed stop every later stop of it in the same Theseus run hands over and then waits
  forever (`SessionClosing.ShouldClose` is given `alreadyAsked = true`). Seen in the second run; read in the code
  (`_closed` is only added to, lines 44, 1334, 1339).
- **L1 (unconfirmed): closes can come well after the 20 s quiet period.** 87.3 s on the first card. The close step
  runs only in `Refresh()`, which runs on session-registry changes, link changes, edits and F5, not on a timer; a
  session that goes idle produces its last event when it has been quiet for 0 s. That would explain it, but the
  first card's close came without a user action, so something else triggered it; not proved.
- **L2 (UX): a card shows no sign that a stop is under way**, so a stop looks as if nothing happened and invites a
  second press (Nigel did, at 15:19:52).
- Corrected during the check: the implementer first said pressing Focus re-read the sessions. It does not; it only
  makes WPF re-check which buttons are enabled. The Start button that came back after Focus was that, not a refresh.

### Verdict, part 2

**Passes** for what stop-timing adds: delivery is stamped, and finished stops are timed correctly and logged once. The
unfinished path is not yet seen live. L3 is a real defect in stopping, found because of this work; L1 and L2 are
smaller. Whether and how to take them on is Nigel's decision.

---

## Part 1, attempt 2

Pipeline: `feature` (method kit v0), stage **verify, part 1 of 2**, **attempt 2** (a re-verify of the fix round).
Part 2 is a live check on a running board, by Nigel. Autonomy L0. Card git-22, branch `main`, repo Theseus, commit
`ee9a529` (the fix round), compared with `a565dd7`. The first attempt is in `evidence-part1-attempt1.md`.
Verifier: a fresh sub-agent started by the implementer. Independence is partial: same model, briefed by the
implementer, but it did not write the code. It read `implementation.md` only after checks 1 to 3.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | PASS | Total 1465, passed 1465, failed 0, total time 1.5121 min, exit 0. No compiler warning in the output: the only lines containing "warning" are the names of six tests. `Theseus.Core` and `Theseus.Tests` were rebuilt in this run. No CS0009 or file-in-use error, so no rerun. Every line is in `C:\Users\nigel\.claude\logs\git-22.log` under `<stop-timing-verify-2>`, from 14:26:47.513 (banner) to 14:29:12.994 (exit line). The 17 new test cases all ran and passed. |
| 2 | F1 to F7 | PASS | Table below. F1, F3, F5, F6 and F7 are fixed. F2 is mostly fixed and F4 is partly fixed. What is left is stated in `implementation.md` and is reasonable. |
| 3 | Nothing else changed | PASS | `git show ee9a529 --stat` touches only `ShellViewModel.cs`, `StopRequestStore.cs`, `StopTiming.cs`, the two test files and two `work/` notes. `git diff a565dd7 ee9a529 --numstat -- tests/` gives 39 and 67 lines added, 0 removed, so no existing test was changed. Stopping behaviour: `SessionClosing` is not in the diff. The close conditions, the 20 s quiet period, the expiry and `Sweep` are unchanged. In `ShellViewModel.cs:1322` `Retire` is still called under the same condition, and only the report is now guarded by its result. In `Claim`, the move and the parse are unchanged (`StopRequestStore.cs:86-101`). The new `File.Exists` early return (`:111-114`) returns the already-parsed request, so the delivery is unchanged. |
| 4 | `implementation.md` "Pass 2" against what was found | Matches | See the section at the end. |

## F1 to F7

| # | Status | Evidence |
|---|---|---|
| F1 | Fixed | `FinishedText` (`StopTiming.cs:64-85`) builds the delivery part and the handoff part separately. `Finished` (`:17-29`) can produce only four shapes, and each is worded correctly: all times present gives "delivered after X, handoff Y later, /exit Z after that". No delivery time gives "delivery time not recorded, handoff Y after the request, /exit Z after that". Delivery present with no handoff gives "delivered after X, handoff time could not be read", so the delivery time is no longer blamed. Neither time present gives "delivery time not recorded, handoff time could not be read". Each shape has an exact-match test (`StopTimingTests` `AFinishedStop...`). A wrong branch choice would now fail at least one test. |
| F2 | Mostly fixed (a known window remains) | `StopRequestStore.cs:111-116` checks `File.Exists`, then calls `AtomicFile.WriteAllText`, which writes `<id>.json.tmp` and then calls `File.Move(..., overwrite: true)` (`AtomicFile.cs:23-25`). A reader can no longer see half a file. A request removed between the check and the move can still be written back, but the window is now the time to write one small temp file, not the whole write. `implementation.md` says so. New note N1: a failed stamp leaves the `.tmp` file behind. |
| F3 | Fixed | (a) `Claim_StillDeliversWhenTheDeliveryTimeCannotBeWritten`: a read-only file still claims, with a null `DeliveredUtc`. If exceptions escaped `Claim`, it would fail. It cannot tell a catch-all from the old narrow filter, because `UnauthorizedAccessException` was caught by both, but the catch-all is plain to see in the code. (b) The wording and branch tests are listed under F1, plus the four removal reasons and six ages. (c) `ARequestFileWrittenBeforeDeliveryTimesExisted_IsStillRead` writes JSON by hand with no `deliveredUtc` property. If the field were required (`JsonRequired`), deserialising would throw `JsonException`, `Parse` would return null (`:242-245`), and `Is.Not.Null` would fail. So the test can fail on wrong code. |
| F4 | Partly fixed, knowingly | The newer-session site (`ShellViewModel.cs:1322-1325`) reports only if `Retire` succeeded. The finished site (`:1353-1354`) still ignores the result. The pre-existing "Retired..." line (`:1327`) is still unconditional. Leaving these is reasonable: only a failed delete triggers it, the store already logs that failure as a warning (`StopRequestStore.cs:199-203`), and the limit is stated in `implementation.md`. |
| F5 | Fixed | `catch (Exception)` at `StopRequestStore.cs:120`. The `with` copy at `:107` is outside the `try`, but copying a non-null record cannot throw. Nothing thrown while stamping can escape `Claim`. |
| F6 | Partly fixed | Finished lines get "(a negative figure means the clock or a file time moved)" when any figure is negative (`StopTiming.cs:79-82`, with a test). Unfinished lines do not: a request time in the future prints, for example, `-7200s after it was asked`, with no flag and in seconds rather than hours (N2). |
| F7 | Fixed | Ages are truncated (`StopTiming.cs:105-108`). Probe results: 59.9 s gives `59s`, 89.99 s gives `89s`, 90 s and 119 s give `1m`, 3599.9 s gives `59m`, 5399.9 s gives `89m`, 5400 s gives `1.5h`, 8280 s gives `2.3h`, 8640 s gives `2.4h`, 9360 s gives `2.6h` and 15120 s gives `4.2h` (no floating-point drop at the decimal), and 100 h gives `100.0h`. These match the tests' expectations, and the boundaries fall where the doc comment says. Reasons are now in words. The plan's promise of queryable values is withdrawn in `implementation.md`: each line is one `{Outcome}` string. Durations still use `InvariantCulture` (`:143-144`). |

## New findings

| # | Severity | Finding |
|---|---|---|
| N1 | note | When the atomic stamp fails at the move (for example, the read-only case), `<id>.json.tmp` stays in `delivered\`. Probe: after a read-only claim, both `...json` and `...json.tmp` were left, and `Handed()` counted 1. So the leftover is not read as a request, as `implementation.md` says. Nothing ever deletes it, though: `Retire` and `Sweep` handle only `<id>.json`. One leftover per failed stamp, overwritten by the next stamp for the same session. This is harmless clutter. |
| N2 | note | `UnfinishedText` has no flag for a negative age, and `Age` shows any negative value in seconds (`-7200s`), because it is below 90. This is rare: it needs a clock change, or a request time in the future. |
| N3 | note | No test covers the exact 90 s or 90 min boundary (the tests use 89 s and 5399 s). The probe shows these boundaries behave as documented. |

## Verdict, part 1 (attempt 2)

**PASS.** The full suite passes, 1465 of 1465, with no compiler warning. The fix round fixes F1, F3, F5 and F7.
It fixes F2 except for a stated microsecond window, and F4 and F6 in part, knowingly. It removes or changes no
existing test, and it does not change stopping behaviour. The new notes (N1 to N3) are minor and need no action
before the part-2 live check.

## Comparison with implementation.md "Pass 2"

It matches. Every row agrees with the code: the F2 window it states, the `.tmp` leftover not being read (confirmed
by the probe, though it never says the file is never cleaned up), the finished-site `Retire` limit, the withdrawn
structured-properties claim, and 1465 of 1465 with 0 warnings. It does not mention N2 (unfinished lines have no
negative flag), because F6 asked only about the finished line's figures.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify part 1, attempt 2 | 2026-11-18 14:26:27 (observed, `date`) | 2026-11-18 14:29:51 (observed, `date`, before writing this file) | Suite ran 14:26:47.513 to 14:29:12.994 (workstream log). The probe (a throwaway console app in the scratchpad that references the built `Theseus.Core.dll`) ran right after the suite, and its temp folder under `%TEMP%\stop-probe-*` was then deleted. |
