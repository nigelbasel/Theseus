# guard-mentions: verify evidence (attempt 3)

Pipeline: `bug` (method kit v0), stage **verify, attempt 3**, autonomy L0. Card git-22, branch `main`, repo
Theseus. Code verified: pass 3, commit b994bbb.

**How this file was made, so it can be weighed properly.** Verify attempt 3 was run by a fresh sub-agent. It
stopped part way: a safety classifier halted it while it was writing new contrived command text to fool the guard
(its check 4). Nigel then decided (option A) that the guard protects against accidents, not against a session
trying to get round it (spec rule 0 in `fix.md`), and that the verify is judged on what the sub-agent completed.
**This file was compiled by the implementer** from that sub-agent's saved results
(`verify3/results-old.json`, 133 cases) and the workstream log. The verdicts below are the sub-agent's machine
results; only the summary is the implementer's. The sub-agent also saw the rest of `fix.md` earlier than its brief
intended.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `Theseus.slnx` | **PASS** | 1436 of 1436, exit 0, logged under `<guard-mentions-verify-3>` from 13:01:46 to 13:04:25 |
| 2 | Earlier tests unchanged | **PASS** | `git diff f3418f6 HEAD -- .../LockGuardTests.cs` and `git diff de8619a HEAD -- .../LockGuardLockedRunTests.cs` both empty |
| 3 | Replay of every command from attempts 1 and 2, at pass 3, pass 2, pass 1 and the pre-fix guard | **PASS against the spec** | Table below |
| 4 | New contrived attempts to fool the guard | **Not run** | Stopped by the classifier; out of scope under spec rule 0 |
| 5 | New realistic locked runs and mentions beyond attempts 1 and 2 | **Not run** | The replay holds 31 locked runs and 27 mentions from attempts 1 and 2 |
| 6 | Guard rails | **PASS** (replayed) | The 13 guard-rail cases from attempts 1 and 2 all as expected |
| 7 | `fix.md` claims | **Not run by the verifier** | |

## Replay at pass 3 (133 commands)

| Group | Commands | As expected | Not as expected |
|---|---|---|---|
| Locked runs (should be allowed) | 31 | 31 | none |
| Harmless mentions (should be allowed) | 27 | 26 | H9, refused before the fix as well |
| Guard rails | 13 | 13 | none |
| Controls | 3 | 2 | C3, see below |
| Unlocked runs from attempt 1 (should be refused) | 33 | 25 | 8, all allowed by the pre-fix guard too |
| Unlocked runs from attempt 2 (should be refused) | 26 | 26 | none |

Compared with the pre-fix guard: **43 commands it let through are now refused**, and **15 harmless mentions it
refused are now allowed**. Compared with pass 2: the 12 bypasses attempt 2 found are refused again, and M4/M5 (a
locked run with a quoted `NDC_TEST_LOCK_REASON`) are allowed.

### Still not as expected

| Id | Command | Now | Before the fix | Class |
|---|---|---|---|---|
| A6 | `bash L status & dotnet test X.sln` | allow | allow | PRE-EXISTING bypass |
| A8 | `bash /tmp/ndc-test-lock.sh run a b -- dotnet test X.sln` | allow | allow | PRE-EXISTING bypass |
| A9 | `sh ./ndc-test-lock.sh run a b -- dotnet build X.sln` | allow | allow | PRE-EXISTING bypass |
| A14 | `bash L status $(dotnet test X.sln)` | allow | allow | PRE-EXISTING bypass |
| A15 | ``bash L status `dotnet test X.sln` `` | allow | allow | PRE-EXISTING bypass |
| A16 | `bash L status > /dev/null & docker compose up -d --build api` | allow | allow | PRE-EXISTING bypass |
| P6 | `GB status $(dotnet test X.sln)` | allow | allow | PRE-EXISTING bypass |
| P7 | `& "C:\fake\Git\bin\bash.exe" "C:\fake\ndc-test-lock.sh" run a b -- dotnet test X.sln` | allow | allow | PRE-EXISTING bypass |
| H9 | PowerShell here-string commit message containing an apostrophe and `bash ndc-test-lock.sh run` | refuse | refuse | PRE-EXISTING false refusal |
| C3 | `bash L run a b -- echo \\"; dotnet test X.sln; echo "` | refuse | allow | Refusal accepted by spec rule 3: unsure quoting plus a separator |

(`L` = `~/.claude/bin/ndc-test-lock.sh`, `GB` = Git Bash running `$HOME/.claude/bin/ndc-test-lock.sh` from PowerShell.)

## Gate

The bug pipeline's verify gate: every result passes, the reproduction tests are unchanged, and, by this fix's
spec, no new bypass and no new false refusal that the spec does not accept.

- Reproduction and earlier tests unchanged: **met**.
- Full suite: **met**.
- No new bypass against the pre-fix guard: **met** (every remaining bypass was there before the fix; Nigel set
  them aside as a separate decision).
- No new false refusal outside the spec: **met** (C3 is accepted by rule 3).
- Checks 4, 5 and 7 were not run.

**Verdict: passes on the completed checks, with checks 4, 5 and 7 not run.** Acceptance is Nigel's.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify, attempt 3 | 13:01:19 (sub-agent) | stopped part way; summary compiled after | Sub-agent: 148,497 tokens, 14 tool calls |
