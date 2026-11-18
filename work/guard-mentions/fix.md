# guard-mentions: fix

Pipeline: `bug` (method kit v0), stage **fix**, autonomy L0. Card git-22, branch `main`, repo Theseus.

## Cause

The guard asked "does the command contain the lock script's name?" when the question that matters is "does
the command run the lock script?". Everything followed from that: a mention was refused as a WSL run from
PowerShell, and a mention ended the guard early, so the rest of the command went unchecked.

## Change

`tools/locks/ndc-test-lock-guard.js` only:

- The command is split into segments at newlines, `&&`, `||`, `;` and `|`.
- A segment **runs** the lock script when its program, after any PowerShell `&` and any `VAR=value` prefixes,
  is the script itself, or is `bash`/`sh` (with or without a path or `.exe`) whose first argument is the script.
  (`programAndArgument`, `runsLockScript`.)
- The WSL refusal applies only when such a segment runs from PowerShell and its program is not Git Bash
  (`viaGitBash`, which now checks the program, not the whole command text).
- Every **other** segment is still checked for container suites, compose builds and solution builds. The
  segments that run the lock script are left out of that check, because the command after their `--` is
  exactly what the lock is for.
- When nothing else is refused, a command that runs the script still gets the "lock busy" notice, as before.

The test file is unchanged since it failed (`git diff f3418f6 -- tests/Theseus.Tests/Locks/LockGuardTests.cs` is
empty).

## Why this addresses the cause

The decision is now made on what a segment executes, not on what text appears anywhere in the command. Both
symptoms came from the text test, and both cases now go through the same rule.

## Result

All 80 tests under `Theseus.Tests.Locks` pass (logged at the `<guard-mentions> ===== fix` banner).

## Known limits (for the verifier and the reviewer)

- Splitting ignores quoting: a separator inside a quoted string (for example a commit message containing
  `; bash ndc-test-lock.sh run`) splits the segment there. That can make a quoted mention look like a run.
  It can only cause a needless WSL warning or a lock-busy notice, never let an unlocked run through, because
  every segment that is not a run is still checked.
- Only the repo copy is changed. Sessions run the copy in `~/.claude/bin`, which the installer replaces. It is
  not installed by this stage: installing changes the hook every session on the machine runs, so that is
  Nigel's call.
- The full suite was not run in this stage; the verifier runs it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| fix | 2026-11-18 12:13:17 (logged) | this commit's time | Lock tests passing, logged under `<guard-mentions>` |
