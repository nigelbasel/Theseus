# Review, pull request 1

An independent reviewer read pull request 1 (ACs 1 to 11 and method-v0.2) against the accepted spec, the repo's
rules and the reviewer rubric on 2026-11-20, then re-reviewed the fixes.

**First pass:** one must-fix (a file no branch could give crashed every refresh), three should-fix (git on the UI
thread; the move to v0.2 found off the primary's first-parent line, so parallel merges read as later work; a primary
clone on `main` unreadable until the merge into `main`), seven to consider. All fixed; see `implementation.md`,
"Review fixes".

**Second pass:** every item resolved; one new should-fix (a remembered primary never checked against the file's
`Primary branch` line) and two small ones (a stale queued read; a notice overwritten). All fixed.

**Judged fine:** the project file kept all 37 criteria's text and ticks; the pipelines and `method/project.md` match
departure 5 and AC 3; git is called with an argument list, never fetches, and reads branches as `refs/heads/`; the
background read touches shared state only on the UI thread; a board with no project repositories runs no git (AC 22);
tests run against real throwaway repositories and none is weakened.

**Open for the owner:** listed in `implementation.md`.

# Review, pull request 2

The same reviewer read pull request 2 (ACs 12 to 18, 22, and PR 1's remainders) on 2026-11-20, then re-reviewed twice.

**First pass:** one must-fix (Select all, Invert and Select live reached the project's hidden cards, so Stop or
Archive selected acted on cards not shown), two should-fix (project cards on the grid for seconds after launch until
every slug was read; a stale comment), six to consider. All fixed.

**Second pass, with the artefact cache added to remove the background read's cost:** every item resolved; two new
should-fix (ticked cards leaving the grid still ticked; a tag named like a branch broke the cache's tips) and three to
consider (cache growth, reading at the snapshotted commit, "reading…" before the first artefacts). All fixed and
tested; see `implementation.md`.

**Judged fine:** AC 22 holds (no readable project, no change on screen apart from AC 17's label); unreadable,
pinned and unlisted projects return their cards to the grid; only the owner's Yes writes the acceptance store and no
tool, hook or file reaches it; the view is its own control and every command is on the menu bar (R16); the cache is
keyed by commit and tree ids, so rewinds and merges stay correct.

**For the owner to confirm:** one failed read keeps the last good reading (AC 13 softened by one read).

# Review, pull request 3

The same reviewer read pull request 3 (ACs 19 to 22) on 2026-11-20. **Must-fix:** none. **Should-fix, both fixed:**
Bring window forward was disabled after a resume reminted as a fresh session (runs now matched by workstream key);
a Start before the first catalogue landed missed the project window (the last readable projects are remembered).
**Consider, both fixed:** the run log read twice per refresh on the UI thread; an area that could share a project's
window name. **Judged fine:** one launch path records every window; old run logs load; with no readable project every
window name is exactly as before (AC 22); Bring window forward never raises an unrelated window; the evidence does not
claim the holdout scenario.
