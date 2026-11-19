# resumed-stop: reproduction

Pipeline: `bug` (method kit v0), stage **reproduce**, autonomy L0. Branch `main`, repo Theseus.
Chosen by Nigel on 2026-11-18 as an extra stage 0 change, from L3 in `work/stop-timing/evidence.md`, taking with it
F1, F3 and F4 from `work/stop-timing/review.md`.

## The report

Seen live on 2026-11-18 (`work/stop-timing/evidence.md`, part 2): the card "Stop check: stays idle" was stopped and
closed (15:19:53), started again, which resumed the same Claude Code session id, and stopped again (15:23:06). The
second stop was delivered and the session wrote its handoff (15:23:18), but Theseus never typed `/exit`: nothing was
logged for eleven minutes, through Focus, F5 and the Refresh button. Nigel typed `/exit` himself.

## Cause

`ShellViewModel` remembered the sessions it had typed `/exit` into in a set keyed by **session id**, and nothing ever
removed an entry. A resumed session keeps its id, so every later stop of it, for the rest of that Theseus run,
reached `SessionClosing.ShouldClose` with `alreadyAsked = true` and was never closed.

## Expected, and where that comes from

`/exit` is typed once per **stop**, not once per session for the life of the board. The record exists to stop a
second `/exit` landing in a session that has moved on (its comment, and the 2026-09-02 incident in
`CloseFinishedSessions`), not to stop a later, separately asked stop from finishing. `SessionClosing.StartedSince`
already treats "asked after this process started" as the test for whether a request belongs to the session running
now.

## How it was made testable (Nigel's option A)

The record lived in a private field of an untested view model. Commit **f9b82e3** moved it, unchanged, into Core as
`ExitsTyped` (keyed by session id, never cleared), with three characterisation tests, and the whole suite otherwise
unchanged. This is a refactor before the failing test, which the bug pipeline did not yet allow for; noted as a
lesson.

## The failing test

`tests/Theseus.Tests/Stopping/ExitsTypedTests.cs`, `ALaterStopOfTheSameResumedSession_CanStillBeClosed`:

```
Failed ALaterStopOfTheSameResumedSession_CanStillBeClosed
  Expected: False
  But was:  True
```

Logged under `<resumed-stop>` at the reproduce banner: 1474 tests, 1473 passed, this one failed.

## Gate

The new test fails for the reported reason on the unfixed code: **met**.

## For the rule stage

The fix needs a rule the code does not state, so the bug pipeline's `rule` stage applies. Questions it must answer,
from the stop-timing review:

- **F1:** Stop pressed again on a session whose earlier stop was delivered but not closed. Today a new request is
  written and the next claim silently replaces the delivered one; the earlier stop is lost without a record.
- **F3:** a re-ask rewrites `RequestedUtc`, so the stop's figures run from the last press.
- **F4:** the `Retire` after a typed `/exit` ignores its result; if the file stays, the stop is later reported as
  unfinished although `/exit` was typed.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| reproduce | 2026-11-18 16:39:55 (logged) | this commit's time | Includes the refactor commit f9b82e3 (16:44:25) |
