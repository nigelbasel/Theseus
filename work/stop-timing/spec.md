# stop-timing: spec

Pipeline: `feature` (method kit v0), stage **spec**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Scope: option B of `research.md`, chosen by Nigel on 2026-11-18.

## Goal

Every stop leaves a record in the Theseus log of how long it took, or, if it never finished, how far it got.
The record is for measuring stopping (in the first place, to set the holdout scenario's stop deadline from data)
and for seeing why a stop got stuck.

## Acceptance criteria

**When the request reaches the session**

1. When the stop hook hands a request to its session, the delivered request records the moment it was handed
   over (`DeliveredUtc`), read from the clock at that moment.
2. If that time cannot be written, the request is still delivered exactly as today. Recording the time never
   stops, delays or changes a stop.

**When a stop finishes**

3. When `/exit` is typed into a session that was asked to stop, the Theseus log gets one Information line for that
   stop, naming the workstream and giving, in seconds:
   - from the request to its delivery (the session's turn ending),
   - from delivery to the handoff being written,
   - from the handoff to `/exit`,
   - and the total from the request to `/exit`.
4. A request delivered without a recorded delivery time (one claimed by a hook installed before this change) still
   gets the line, with the total and the time to the handoff, and says the delivery time was not recorded.

**When a stop does not finish**

5. Whenever a stop request is removed without `/exit` being typed, the Theseus log gets one Information line for
   that request, naming the workstream, how long ago it was requested, why it was removed, and how far it got:
   - **never delivered**: the session never ended a turn after the request;
   - **delivered, no handoff**: delivered, but no handoff written since the request;
   - **handed over, not closed**: a handoff written since the request, but `/exit` never typed.

   This covers each way a request can be removed today: withdrawn or retired because its card is being started
   again; retired because the session now running started after the request; dropped when Nigel declines the stops
   held back at startup; and expired after four hours. The expiry, which today logs only a count at Debug level,
   logs one line per request.
6. A stop removed after `/exit` was typed is not reported again as unfinished.

**Unchanged**

7. Stopping behaves exactly as before: the same conditions for typing `/exit`, the same 20-second quiet period, the
   same four-hour expiry, the same delivery. Only what is recorded changes.
8. Every duration comes from times recorded on the machine (the request, the delivery, the handoff file's write
   time, the moment `/exit` is typed or the request is removed). None is estimated. A time that is missing is said
   to be missing, not filled in.
9. The records stay in the Theseus log on this machine. No new file, and nothing in the board's UI.

## Out of scope

- A stop history file and a stop time shown on the board (option C).
- Retrying or surfacing a stuck stop. Whether to do either is decided once these records show which kind of stuck is
  common.
- Setting the holdout scenario's stop deadline. That is done later, from the data this produces.

## Holdout scenarios that apply

`stop-with-handoff` (by name only; the implementer does not read it).

## How it will be verified

- Unit tests in `Theseus.Core` for: the durations and the line for a finished stop, with and without a delivery
  time; how far an unfinished stop got, for each of the three answers; and the hook's claim recording the delivery
  time without ever failing the delivery.
- The full suite.
- On a running board, by someone other than the implementer: stop a test card that hands over, and stop one that
  cannot (for example, one left idle), then read the Theseus log for the lines above.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| spec | 2026-11-18 13:56:17 (logged) | this commit's time | Gate: Nigel approves this spec |
