# handoff-names: implementation

Pipeline: `feature`, stage **implement**, from `plan.md`, reworked after `review.md` (AC numbers are the spec's after
that rework).

| AC | Where |
|---|---|
| 1 | `HandoffOwner.Of`: the one sharer the file is named exactly after, by key or title as a key |
| 2, 4, 5 | `SharedHandoffs.Plan` decides the moves and their notice (`HandoffMove.Notice`), tested in `SharedHandoffsTests`; `ShellViewModel.ResolveSharedHandoffs` writes the pointer files, persists each card and shows the notices. A failed write is logged once per file |
| 3 | `HandoffBusy.Of`: live, or a run not ended, or one begun within the launch window, tested in `HandoffBusyTests` |
| 5 (repository) | `GitWorkingTree.Contains`, tested on temp folders |
| 6 | `HandoffRename.Offered` (busy, shared, missing, taken), set on each card as `HandoffRenameTo`; the menu item shows only when set |
| 7 | `ShellViewModel.RenameHandoff` re-checks the offer against the board now, then `HandoffRename.Apply` (one move, never replacing a file), tested on a temp folder |
| 8 | Keys are untouched |

Review findings: M1 (AC 3), M2 (AC 4), M3 (real names removed from tests and records), S1 and S2 (exact naming), S3
(AC 5), S4 (AC 6), S5 (repository check), S6 (one warning per file), S7 (the decisions moved to Core and tested).

Live check (for Nigel, after installing): the pair of cards on his board that share a chosen file resolves, the second
card getting `HANDOFF-<its key>.md` with the pointer line; a card whose handover file is named for an old key offers
the rename while its session is stopped.
