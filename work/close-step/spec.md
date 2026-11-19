# close-step: spec

Pipeline: `feature` (method kit v0, P1 to P18), stage **spec**, autonomy L0. Branch `nigel/close-step`, repo Theseus.
From `research.md`. **Awaiting Nigel's approval.**

## What changes, from the owner's side

A stopped session closes about 20 seconds after it has handed over and gone quiet, whether or not anything else
happens on the board. While a stop is under way, its card says so and says how far it has got. A stop the board
cannot finish says why, and nothing types into a terminal again unless the owner asks.

## Acceptance criteria

**When the close happens (L1)**

1. A session that meets every close condition except quiet is closed once its quiet period is reached, with no other
   activity on the board: within 5 s of `SessionClosing.Quiet` (20 s) after it went idle.
2. Nothing is scheduled while no stop is pending. At most one re-check is pending at a time, for the earliest moment
   any session's quiet period ends; a refresh in between neither adds a second one nor types twice.
3. The re-check runs the close step only, not a full board refresh, on the UI thread.
4. The four close conditions are unchanged: asked, idle, handoff written since the request, quiet for 20 s.

**What the card shows (L2)**

5. From the moment a stop is asked until the session exits or the request is retired, the card's Stop icon is replaced
   by a **stopping** icon in the board's amber (`#D68910`). It cannot be pressed, so a stop cannot be asked twice from
   the card.
6. Its tooltip says where the stop has got to, at the latest stage reached, with local times:
   - *Asked at HH:mm. Waiting for its turn to end.*
   - *Handed over at HH:mm. Writing its handoff.*
   - *Handoff written at HH:mm. Closes once it has been quiet for 20 s.*
   - *Typed /exit at HH:mm. Closing.*
   - *Could not type /exit: <reason>.* (see 9)
7. A card with no stop under way looks and behaves exactly as today, checked against what is rendered (P11). This
   applies to every card, ad hoc or project.

**Which run a stop is for (V1)**

8. The close step decides whether a stop belongs to an earlier run of the session by when it was **handed over**
   (`SessionClosing.HandedBefore`), the same rule the Stop press uses. A stop handed to a session that was resumed
   outside the board's Start is closed, not retired.

**An /exit that did not land (V2)**

9. When `/exit` could not be typed, nothing retries it on its own: each attempt raises the terminal and selects the
   tab (`TabNudge.Nudge` via `TerminalFocus.FocusSession`), so an automatic retry would take focus from the owner
   without warning. Instead the card shows the reason:
   - **Tab not confirmed** or **terminal not in front** (nothing was typed): the card offers **Close now**, which
     tries once, only when pressed, under the same four conditions.
   - **Refused** (Windows may have taken some of the keystrokes) or **blocked by elevation**: *Close it yourself*,
     and no button. Neither is retried, ever: a partial `/exit` followed by a second one would be typed into the
     prompt.
10. The banner notice for a second Stop press no longer says the session will close when its last attempt failed; it
   says what the card says.
11. `/exit` is never typed twice into a session once it was sent. (Unchanged; still tested.)

**Records**

12. The existing log lines stay. A re-check that closes a session logs the same "Closing ..." line as today; nothing
   is logged for a re-check that finds nothing to do.

## Out of scope

- The parked-cores blink (separate small change, next).
- How a session is asked to stop (`StopRouting`, the hook, the request text), the held-back flow at startup, and
  `Sweep`'s expiry.
- Project rows and terminal windows per project (objective 1.1).

## Holdout scenarios that apply

`stop-with-handoff`. Under P18 and Nigel's decision (2026-11-19, option A): close-step's verify stays open until the
verifier card (objective 1.0) has run it, and the pull request is not merged before then.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| spec | 2026-11-19, after research (a25becf); `TabNudge` read first (P4) | this commit's time | |
