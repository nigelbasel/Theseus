# resumed-stop: fix

Pipeline: `bug` (method kit v0), autonomy L0. Card git-22, branch `main`, repo Theseus.

## Spec (stage **rule**, for Nigel's approval)

What the code protects against: a second `/exit`, or a second stop request, landing in a session that is already
finishing or has moved on; and a stop that is never finished. When unsure, it does not type into a session.

1. **One stop is one request**, from the press until `/exit` is typed or the request is removed. A stop is known
   by its session and the time it was asked.
2. **`/exit` is typed at most once per stop, and a later stop can be finished.** A stop asked after the last `/exit`
   (a resumed session under the same id) is a new stop, and is closed in the usual way. (L3)
3. **Pressing Stop on a session whose stop has already been delivered does not ask again.** The session has been
   told; telling it twice can interrupt it while it writes the handoff. Instead the press runs the close check at
   once, so a session that has handed over and gone quiet is closed on the press, and otherwise the notice says the
   stop is under way. (F1; and in part L2, a press that seems to do nothing.)
4. **A stop's time is the first press.** Asking again while the request is still waiting keeps its original request
   time, so a stop's figures run from when it was first asked. (F3)
5. **A stop counts as finished once `/exit` has been typed for it.** If its request cannot then be removed, a later
   removal of that request in the same board run is not reported as unfinished. After a restart, the leftover
   request is handled as today (held back and asked about). (F4)
6. **Unchanged:** the routes for a request still waiting (interrupt a working session on a second press, nudge an
   idle one), the close conditions and quiet period, expiry, and what is logged.

Out of scope: L1 (closes that wait for an unrelated refresh) and the rest of L2 (showing on the card that a stop is
under way). Rule 3 helps both on a second press only.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| rule | 2026-11-18 16:48:58 (logged) | this commit's time | Gate: Nigel approves the spec above |
