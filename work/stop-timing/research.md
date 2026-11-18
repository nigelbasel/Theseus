# stop-timing: research

Pipeline: `feature` (method kit v0), stage **research**, autonomy L0. Card git-22, branch `main`, repo Theseus.
No code changes in this stage.

## The ask

Record how long a stop takes, from Stop being pressed to the handoff being written and the session closing. It
exists to replace a placeholder with a measured figure: the holdout scenario "stop with handoff" needs a
deadline, and that figure was deliberately not invented.

## What exists today

A stop passes through four moments. What Theseus records of each:

| Moment | Where it happens | Recorded? |
|---|---|---|
| 1. Stop requested | `ShellViewModel` writes a `PendingStopRequest` to `stop-requests\` | Yes: `RequestedUtc`, and the log line "Asked {Workstream} ({SessionId}) to stop" |
| 2. Request delivered | `Theseus.Hook` at the end of the session's current turn; `StopRequestStore.Claim` moves the file to `stop-requests\delivered\` | **No.** `File.Move` keeps the file's original time, and nothing is logged (the hook runs in the session's process) |
| 3. Handoff written | The session writes its handoff file | Only as the file's write time, read by `SessionClosing.ShouldClose` to check it is this stop's handoff |
| 4. `/exit` typed | `ShellViewModel.CloseFinishedSessions`, once the session has been idle for `SessionClosing.Quiet` (20 s) after a handoff newer than the request | The log line "Closing {Workstream}, which was asked to stop and has handed over: {Outcome}", with no durations; a `RunEnd` with `ClosedCleanly` |

A stop that never reaches moment 4 leaves no record of where it stopped. Its request either expires after four
hours (`Sweep`, logged only as a count: "Discarded N stop request(s)") or is retired when the card is started
again ("Retired the delivered stop request for {Workstream}, which is being started again").

## What the existing logs already show

The Theseus logs on this laptop (`%LOCALAPPDATA%\Theseus\logs`) hold every stop since 2026-11-03. Pairing
"Asked ... to stop" with "Closing ..." per workstream (names withheld; this repo is public):

| Date | Stops asked | Closed | Time from request to `/exit` | Never closed |
|---|---|---|---|---|
| 2026-11-03 | 2 | 0 | | 2: one withdrawn when its card was started again after 20 min; one discarded unanswered at expiry |
| 2026-11-11, 13:45 (nine sessions stopped together) | 9 | 6 | 4 at 53.6-55.3 s; 2 at 97.0-97.4 s | 3: still delivered-but-unfinished at 17:46, when their cards were started again |
| **Total** | **11** | **6** | **53.6-97.4 s** | **5** |

What this means:

- When a stop completes, it has taken about 1 to 1.5 minutes from the press, of which at least 20 s is the
  deliberate quiet period before `/exit` is typed. Six stops are too few to set a deadline from, and all six
  were on one day.
- **Almost half the stops did not complete, and nothing says why.** For the three on 2026-11-11 the request had
  been delivered (they were in `delivered\` when retired), so each session was told; whether it then failed
  to write its handoff, wrote it but never went idle, or was waiting on a question is not recorded. That is a
  bigger gap than the missing durations, and it is exactly the failure the holdout scenario is meant to catch.

## Options

| Option | What it adds | Size |
|---|---|---|
| A. Durations at close | When `/exit` is typed, log one line with the time from request to handoff and from request to `/exit`. Uses data already in hand | Small: one Core function to compute and word it, one log call |
| **B. A, plus where unfinished stops got to** (recommended) | Record moment 2 (the hook stamps the delivered request with `DeliveredUtc` when it claims it). When a request is retired or expires without a close, log how far it got: never delivered; delivered, no handoff; handoff written, never idle long enough to close. The close line then splits request-to-delivery (the session's turn ending) from delivery-to-handoff | Small to medium: an optional field on `PendingStopRequest`, `Claim` rewriting it, one Core function that classifies a stop, two log calls |
| C. B, plus a stop history file and a figure on the board | Every stop appended to a history file (as `locks-history.jsonl` does for locks), and the board showing typical stop time | Medium; the board part is a design question of its own |

## Recommendation

**B.** A alone would time the stops that work and stay silent about the ones that do not, which on this
evidence is nearly half of them. B answers the question that matters for the scenario: not only how long a stop
takes, but where one gets stuck. C can follow once B has gathered a few weeks of data.

Not proposed: changing how stopping works. Whether a stuck stop should be retried or surfaced on the board is a
separate decision, better made once B shows which kind of stuck is common.

## Constraints found

- The delivery happens in `Theseus.Hook`, a separate short-lived process that must keep working while Theseus is
  down. Any change there must stay small and must never make the hook fail (it already swallows exceptions).
- Times written to the log must be measured, never estimated (this repo's method lesson from stage 0).
- Workstream names in the Theseus log are the user's own and stay on the machine; nothing here changes that.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| research | 2026-11-18 13:53:09 (logged) | this commit's time | Read `Theseus.Core/Stopping`, `Theseus.Hook`, `ShellViewModel` close step, and the local Theseus logs |
