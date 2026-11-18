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

## Pass 2 (after the verify gate failed)

The first verifier (`evidence-1-failed.md`) found that pass 1 **refused correct locked runs** whose own quoted
arguments held a separator, for example `... run "s" "suite" -- bash -c "cd x && dotnet test X.sln"` (its L4,
L5, L10, L11). The old guard allowed them, so this was a regression. Pass 1's notes were also wrong: they said a
quoted separator could only cause "a needless WSL warning", and from PowerShell it was a refusal (its F13), and
they did not list the bypasses that remain.

Change in pass 2: `splitOutsideQuotes` splits only at separators outside single or double quotes (a backslash
escapes inside double quotes). New tests in `tests/Theseus.Tests/Locks/LockGuardLockedRunTests.cs` cover all 11 of
the verifier's locked runs and F13; 5 failed before the change (logged at the pass 2 banner) and pass after it.
The reproduction tests in `LockGuardTests.cs` are still unchanged since they failed. All 92 lock tests pass.

## Known limits (for the verifier and the reviewer)

- **Bypasses that remain, all present before this fix** (found by the first verifier, not closed here because
  closing them changes what the guard is meant to cover; Nigel to decide separately):
  - a single `&` after a lock-run segment (`bash <script> status & dotnet test X.sln`);
  - command substitution inside a lock-run segment (`$(...)` or backticks);
  - any path ending in `ndc-test-lock.sh` counts as the script (`/tmp/ndc-test-lock.sh`, a fake `Git\bin\bash.exe`).
  The common cause: whatever follows the script inside its own segment is never checked.
- A segment that does **not** run the script is still checked as text, quotes and all, so a commit message that
  contains `dotnet test X.sln` is still refused. That was true before this fix as well.
- Quote handling is bash-like. PowerShell's backtick escape is not treated as an escape.
- Only the repo copy is changed. Sessions run the copy in `~/.claude/bin`, which the installer replaces. It is
  not installed by this stage: installing changes the hook every session on the machine runs, so that is
  Nigel's call.
- The full suite was not run in this stage; the verifier runs it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| fix, pass 1 | 2026-11-18 12:13:17 (logged) | 12:15:36 (commit 7036ae2) | Lock tests passing; the verify gate then failed |
| verify, attempt 1 | 12:18:40 | 12:23:19 | Sub-agent: 133,237 tokens, 16 tool calls. Gate failed: 4 new false refusals |
| fix, pass 2 | 12:43:55 (logged) | this commit's time | Quote-aware split; 92 lock tests pass |
