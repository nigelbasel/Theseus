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
