# guard-mentions: fix

Pipeline: `bug` (method kit v0), stage **fix**, autonomy L0. Card git-22, branch `main`, repo Theseus.

## Cause

The guard asked "does the command contain the lock script's name?" when the question that matters is "does
the command run the lock script?". Everything followed from that: a mention was refused as a WSL run from
PowerShell, and a mention ended the guard early, so the rest of the command went unchecked.

## Change

`tools/locks/ndc-test-lock-guard.js` only:

- The command is split into segments at newlines, `&&`, `||`, `;` and `|`: outside quotes when the quoting is
  simple, and regardless of quotes when it is not (passes 2 and 3, below; pass 1 always ignored quotes).
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

## Spec for pass 3: fail closed when unsure (Nigel chose option A, 2026-11-18)

Written before the pass 3 code, after two failed verifies showed the guard needed a rule for commands whose
quoting it cannot be sure of.

1. **A wrong refusal is cheap; a wrong allow is not.** A refused command costs a rephrase. An unlocked suite
   running beside a locked one costs a run that fails like a real defect. When in doubt, the guard refuses.
2. **Quotes are honoured only when the quoting is simple:** every `"` and `'` is balanced, and the command
   contains none of `\"`, `\'`, a backtick, `$'`, `#`, `@'` or `@"`. Those are where bash and PowerShell disagree
   with each other or with a plain reading, so the guard cannot know how the shell will split the line.
3. **When the quoting is not simple, the guard splits as if there were no quotes** (pass 1's behaviour): a
   separator anywhere ends a segment. A quoted separator may then cause a refusal; it can never hide a command.
4. **A `VAR=value` prefix may have a quoted value** (`NDC_TEST_LOCK_REASON="two words"`), as the machine's
   instructions tell sessions to write it.
0. **What the guard is for (added after verify 3, Nigel, 2026-11-18).** Sessions obey the guard. It protects
   against *accidents*: a cooperating session's ordinary command being misread, so that a correct command is
   refused or an unlocked run goes unnoticed. It is not a boundary against a session trying to get round it.
   Verification therefore asks whether the commands sessions really write are read correctly, not whether some
   contrived command text can fool it.
5. Unchanged: a segment that runs the script is a locked run; every other segment is checked; WSL bash is refused
   from PowerShell; the three bypasses listed under Known limits stay out of scope.

## Pass 3 (after the second verify failed)

The second verifier (`evidence-2-failed.md`) found that pass 2's quote-aware split could be fooled: a `"` or `'`
that the shell does not treat as a quote (`\"`, `\'`, a quote in a `#` comment, `$'...'`, PowerShell's backtick,
and `\"` read as an escape in PowerShell, where it is not one) made the guard think a quote was open, so the next
`;` or newline did not split, and an unlocked command hid inside the lock-run segment: 12 bypasses that pass 1
refused (the pre-fix guard allowed them too). It also found that a quoted `VAR="two words"` prefix made a correct
locked run look like something else (M4, M5), in pass 1 and pass 2 alike.

Change in pass 3, per the spec above: `quotingIsSimple` decides whether quotes can be honoured; when not, the
command is split as if there were no quotes. The `VAR=value` prefix may be quoted. New tests in
`tests/Theseus.Tests/Locks/LockGuardUnsureQuotingTests.cs`: the 12 bypasses must be refused, and three locked runs
with a quoted reason must be allowed. All 15 failed against pass 2 (logged at the pass 3 banner) and pass now.
`LockGuardTests.cs` (since f3418f6) and `LockGuardLockedRunTests.cs` (since de8619a) are unchanged. 107 lock
tests pass.

What pass 3 knowingly accepts: a correct locked run whose arguments use any of the unsure constructs **and** a
separator may be refused. That is the trade the spec makes.

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
  **Accepted as limits** (Nigel, 2026-11-19), under spec rule 0: the guard protects against accidents by cooperating
  sessions, and none of these is how such a session writes a test run by accident. Revisit only if one appears in a
  real transcript.
- A segment that does **not** run the script is still checked as text, quotes and all, so a commit message that
  contains `dotnet test X.sln` is still refused. That was true before this fix as well.
- Quotes are honoured only when the quoting is simple (spec rule 2). Otherwise a quoted separator can refuse a
  correct run; it cannot hide a command. A PowerShell here-string commit message mentioning the script (the
  second verifier's H9) is refused, as it was before this fix.
- Only the repo copy is changed. Sessions run the copy in `~/.claude/bin`, which the installer replaces. It is
  not installed by this stage: installing changes the hook every session on the machine runs, so that is
  Nigel's call.
- The full suite was not run in this stage; the verifier runs it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| fix, pass 1 | 2026-11-18 12:13:17 (logged) | 12:15:36 (commit 7036ae2) | Lock tests passing; the verify gate then failed |
| verify, attempt 1 | 12:18:40 | 12:23:19 | Sub-agent: 133,237 tokens, 16 tool calls. Gate failed: 4 new false refusals |
| fix, pass 2 | 12:43:55 (logged) | 12:46:29 (commit de8619a) | Quote-aware split; 92 lock tests pass |
| verify, attempt 2 | 12:47:08 | 12:54:27 | Fresh sub-agent: 162,613 tokens, 17 tool calls. Gate failed: 12 new bypasses vs pass 1, M4/M5 false refusal |
| fix, pass 3 | 12:57:07 (logged) | this commit's time | Spec written first; fail closed when unsure; 107 lock tests pass |
