# guard-mentions: verify evidence

Pipeline: `bug` (method kit v0), stage **verify**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Verifier: a sub-agent started by the implementer (partial independence: same machine, same parent session; it
did not write the fix and read `fix.md` only after checks 1 to 5).

Commits checked: `f3418f6` (failing tests), `7036ae2` (fix). HEAD at verification: `7036ae2`.

## Method

- The guard was driven directly: `node tools/locks/ndc-test-lock-guard.js` with JSON built by a node harness
  (`String.raw` for backslashes, never shell escaping) and `THESEUS_LOCKS_URL=http://localhost:1/locks`, so no
  real lock server was reached. A result is "deny" when stdout contains `"permissionDecision":"deny"`.
- Every command was fed to the fixed guard and to the pre-fix guard (`git show 7036ae2~1:tools/locks/ndc-test-lock-guard.js`,
  saved to the scratchpad as `guard-old.js`), so each miss could be classed NEW or PRE-EXISTING.
- Classes: **OK** (decision as expected); **PRE-EXISTING bypass** (unlocked run allowed by both versions);
  **NEW bypass** (allowed by the fix, refused before it); for commands that should be allowed, **NEW** or
  **PRE-EXISTING false refusal** on the same basis.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | PASS | 1409 total, 1409 passed, 0 failed, 0 skipped, exit 0, "Total time: 1.7695 Minutes". Build did not fail; no rerun needed. Logged line by line in `C:\Users\nigel\.claude\logs\git-22.log` under `<guard-mentions-verify>`, from 12:19:29.400 to 12:21:46.469. |
| 2 | Reproduction tests unchanged | PASS | `git diff f3418f6 HEAD -- tests/Theseus.Tests/Locks/LockGuardTests.cs` is empty (0 lines). |
| 3 | Adversarial: unlocked runs past the fixed guard | PASS for the gate (no NEW bypass); 9 PRE-EXISTING bypasses | 32 adversarial commands (20 Bash, 12 PowerShell) plus A21. 23 refused as they should be, 12 of them were let through by the pre-fix guard (the reported defect, now fixed). 9 still get through, all also through the pre-fix guard: single `&`, `$(...)`, backticks, and a lock-script path that is not the real script (`/tmp/...`, `./...`, `C:\fake\...`). |
| 4 | False refusals of harmless mentions | PASS | 12 commands (F1 to F12, 5 Bash, 7 PowerShell): none refused. 6 PowerShell ones were refused by the pre-fix guard. |
| 4a | Legitimate locked runs still allowed (added by the verifier) | **FAIL** | 11 locked runs. 7 allowed. **4 are refused by the fix and were allowed before it (NEW)**: a locked run whose own arguments contain a separator inside quotes, e.g. `bash ~/.claude/bin/ndc-test-lock.sh run "s" "suite" -- bash -c "cd x && dotnet test X.sln"`. The split ignores quotes, so the text after the quoted `&&` is checked as an unlocked segment. |
| 5 | Guard rails | PASS | WSL `bash` lock run from PowerShell refused (G1, P8, P9); Git Bash allowed (G2, G10 for `usr\bin`, L6); `Theseus.slnx` exempt for test and build (G3, G4); `NDC_TEST_LOCK_BYPASS=1` honoured (G5); compose build and `up --build` refused (G6, G7); plain `dotnet test X.sln` refused (G8); unit suite allowed (G9). |
| 6 | `fix.md` claims match what was found | **PARTIAL** | Cause, change and the unchanged test file match the code and checks 1 to 5. Its "known limits" understate the quoting limit: (a) it says a quoted separator "can only cause a needless WSL warning or a lock-busy notice", but from PowerShell it is a refusal (F13, also refused before the fix, so not new); (b) it does not mention that the same limit refuses legitimate locked runs (check 4a, new). Its claim that the split can "never let an unlocked run through" holds for non-run segments, but the rest of a run segment is never checked, which is how the 9 pre-existing bypasses get through; `fix.md` does not list them. |

## Commands

Tool is the `tool_name` fed to the guard. "Fix" is the decision of `7036ae2`, "Before" of `7036ae2~1`.
`L` stands for `~/.claude/bin/ndc-test-lock.sh`; `GB` for `& "C:\Program Files\Git\bin\bash.exe"`; `\n` and
`\r\n` are real line breaks in the command.

### Adversarial (expected: deny)

| Id | Tool | Command | Fix | Before | Class |
|---|---|---|---|---|---|
| A1 | Bash | `echo L && dotnet test X.sln` | deny | allow | OK |
| A2 | Bash | `echo L; dotnet test X.sln` | deny | allow | OK |
| A3 | Bash | `echo L \|\| dotnet test X.sln` | deny | allow | OK |
| A4 | Bash | `echo L \| dotnet test X.sln` | deny | allow | OK |
| A5 | Bash | `echo L\ndotnet test X.sln` | deny | allow | OK |
| A6 | Bash | `bash L status & dotnet test X.sln` | allow | allow | PRE-EXISTING bypass |
| A7 | Bash | `bash ndc-test-lock.sh; dotnet test X.sln` | deny | allow | OK |
| A8 | Bash | `bash /tmp/ndc-test-lock.sh run a b -- dotnet test X.sln` | allow | allow | PRE-EXISTING bypass |
| A9 | Bash | `sh ./ndc-test-lock.sh run a b -- dotnet build X.sln` | allow | allow | PRE-EXISTING bypass |
| A10 | Bash | `bash -c "L status; dotnet test X.sln"` | deny | allow | OK |
| A11 | Bash | `grep ndc-test-lock.sh README.md && dotnet build X.sln` | deny | allow | OK |
| A12 | Bash | `FOO=1 bash L status && docker compose build api` | deny | allow | OK |
| A13 | Bash | `cat ndc-test-lock.sh; dotnet test Foo.PersistenceTests` | deny | allow | OK |
| A14 | Bash | `bash L status $(dotnet test X.sln)` | allow | allow | PRE-EXISTING bypass |
| A15 | Bash | ``bash L status `dotnet test X.sln` `` | allow | allow | PRE-EXISTING bypass |
| A16 | Bash | `bash L status > /dev/null & docker compose up -d --build api` | allow | allow | PRE-EXISTING bypass |
| A17 | Bash | `"ndc-test-lock.sh" status && dotnet test X.Api.Tests` | deny | allow | OK |
| A18 | Bash | `./ndc-test-lock.sh status\r\ndotnet test X.sln` | deny | allow | OK |
| A19 | Bash | `node ndc-test-lock.sh && dotnet test X.sln` | deny | allow | OK |
| A20 | Bash | `bash L run a b -- echo hi; NDC=1 dotnet test X.sln` | deny | allow | OK |
| A21 | Bash | `bash L run s s -- dotnet build X.sln && dotnet test X.sln` | deny | allow | OK |
| P1 | PowerShell | `Write-Output "ndc-test-lock.sh"; dotnet test X.sln` | deny | deny | OK |
| P2 | PowerShell | `GB "$HOME/.claude/bin/ndc-test-lock.sh" status; dotnet test X.sln` | deny | allow | OK |
| P3 | PowerShell | `cmd /c "bash L status & dotnet test X.sln"` | deny | deny | OK |
| P4 | PowerShell | `Start-Process bash -ArgumentList "L run a b -- dotnet test X.sln"` | deny | deny | OK |
| P5 | PowerShell | `wsl L run a b -- dotnet test X.sln` | deny | deny | OK |
| P6 | PowerShell | `GB "$HOME/.claude/bin/ndc-test-lock.sh" status $(dotnet test X.sln)` | allow | allow | PRE-EXISTING bypass |
| P7 | PowerShell | `& "C:\fake\Git\bin\bash.exe" "C:\fake\ndc-test-lock.sh" run a b -- dotnet test X.sln` | allow | allow | PRE-EXISTING bypass |
| P8 | PowerShell | `& bash L run a b -- dotnet test X.sln` | deny | deny | OK |
| P9 | PowerShell | `bash.exe L run a b -- dotnet build X.sln` | deny | deny | OK |
| P10 | PowerShell | `Select-String ndc-test-lock.sh x.md\ndocker compose build api` | deny | deny | OK |
| P11 | PowerShell | `GB "$HOME/.claude/bin/ndc-test-lock.sh" status && dotnet test X.sln` | deny | allow | OK |
| P12 | PowerShell | `bash -c "L run a b -- dotnet test X.sln"` | deny | deny | OK |

### Harmless mentions (expected: allow)

| Id | Tool | Command | Fix | Before | Class |
|---|---|---|---|---|---|
| F1 | Bash | `git commit -m "guard: treat only runs of ndc-test-lock.sh as locked"` | allow | allow | OK |
| F2 | Bash | `grep -n ndc-test-lock.sh tools/locks/*.js` | allow | allow | OK |
| F3 | Bash | `git log --oneline -- tools/locks/ndc-test-lock.sh` | allow | allow | OK |
| F4 | Bash | `cat ~/.claude/bin/ndc-test-lock.sh \| head -20` | allow | allow | OK |
| F5 | PowerShell | `git commit -m "guard: treat only runs of ndc-test-lock.sh as locked"` | allow | deny | OK |
| F6 | PowerShell | `Select-String -Path tools\locks\*.js -Pattern 'ndc-test-lock.sh'` | allow | deny | OK |
| F7 | PowerShell | `Get-Content "$HOME\.claude\bin\ndc-test-lock.sh" -TotalCount 20` | allow | deny | OK |
| F8 | PowerShell | `git show HEAD -- tools/locks/ndc-test-lock.sh` | allow | deny | OK |
| F9 | PowerShell | `git diff HEAD~1 -- tools/locks/ndc-test-lock.sh \| Select-String run` | allow | deny | OK |
| F10 | PowerShell | `rg "ndc-test-lock\.sh" tools` | allow | allow | OK |
| F11 | PowerShell | `Get-ChildItem ~/.claude/bin \| Where-Object Name -eq ndc-test-lock.sh` | allow | deny | OK |
| F12 | Bash | `git diff HEAD~1 -- tools/locks/ndc-test-lock.sh` | allow | allow | OK |
| F13 | PowerShell | `git commit -m "docs; bash ndc-test-lock.sh run is the form"` | deny | deny | PRE-EXISTING false refusal (the quoting limit `fix.md` names) |
| F14 | Bash | `git commit -m "docs; bash ndc-test-lock.sh run is the form"` | allow | allow | OK |

### Legitimate locked runs (expected: allow)

| Id | Tool | Command | Fix | Before | Class |
|---|---|---|---|---|---|
| L1 | Bash | `bash L run "s" "suite" -- dotnet test X.sln --no-build` | allow | allow | OK |
| L2 | Bash | `NDC_TEST_LOCK_WAIT=600 bash L run "s" "suite" -- dotnet test X.Api.Tests` | allow | allow | OK |
| L3 | Bash | `cd /e/x && bash L run "s" "suite" -- dotnet test X.sln` | allow | allow | OK |
| L4 | Bash | `bash L run "s" "suite" -- bash -c "cd x && dotnet test X.sln"` | **deny** | allow | **NEW false refusal** |
| L5 | Bash | `bash L run "s" "a;b" -- dotnet test X.sln` | **deny** | allow | **NEW false refusal** |
| L6 | PowerShell | `GB "$HOME/.claude/bin/ndc-test-lock.sh" run "s" "suite" -- dotnet test X.sln` | allow | allow | OK |
| L7 | Bash | `bash L run --token abc -- dotnet test X.sln` | allow | allow | OK |
| L8 | Bash | `bash L run "s" "suite" -- dotnet test X.sln --filter "FullyQualifiedName~A\|FullyQualifiedName~B"` | allow | allow | OK (the text after the quoted `\|` happens to match no rule) |
| L9 | PowerShell | `$env:NDC_TEST_LOCK_REASON='x'; GB "$HOME/.claude/bin/ndc-test-lock.sh" run "s" "suite" -- dotnet test X.sln` | allow | allow | OK |
| L10 | Bash | `bash L run "s" "suite" -- sh -c 'dotnet build X.sln; dotnet test X.sln --no-build'` | **deny** | allow | **NEW false refusal** |
| L11 | PowerShell | `GB "$HOME/.claude/bin/ndc-test-lock.sh" run "s" "suite" -- bash -c "cd /e/x && dotnet test X.sln"` | **deny** | allow | **NEW false refusal** |

### Guard rails

| Id | Tool | Command | Expected | Fix | Before | Class |
|---|---|---|---|---|---|---|
| G1 | PowerShell | `bash "$HOME/.claude/bin/ndc-test-lock.sh" run "s" "suite" -- dotnet test X.sln` | deny | deny | deny | OK |
| G2 | PowerShell | `GB "$HOME/.claude/bin/ndc-test-lock.sh" status` | allow | allow | allow | OK |
| G3 | Bash | `dotnet test Theseus.slnx --nologo` | allow | allow | allow | OK |
| G4 | PowerShell | `dotnet build Theseus.slnx` | allow | allow | allow | OK |
| G5 | Bash | `NDC_TEST_LOCK_BYPASS=1 dotnet test X.sln` | allow | allow | allow | OK |
| G6 | Bash | `docker compose build api` | deny | deny | deny | OK |
| G7 | PowerShell | `docker compose up -d --build api` | deny | deny | deny | OK |
| G8 | Bash | `dotnet test X.sln` | deny | deny | deny | OK |
| G9 | Bash | `dotnet test Foo.UnitTests` | allow | allow | allow | OK |
| G10 | PowerShell | `& "C:\Program Files\Git\usr\bin\bash.exe" ~/.claude/bin/ndc-test-lock.sh status` | allow | allow | allow | OK |

## Findings

1. **No NEW bypass.** Every unlocked run that gets past the fix also got past the version before it. The fix
   closes 12 of the adversarial bypasses (all the "mention, then separator, then unlocked command" forms) and
   the PowerShell false refusals of harmless mentions, which is the reported defect.
2. **NEW false refusal of legitimate locked runs (L4, L5, L10, L11).** The split at `&&`, `||`, `;`, `|` and
   newlines ignores quotes. When the command after the lock script's `--` is itself a quoted `bash -c "a && b"`
   or `sh -c 'a; b'`, or a label contains `;`, the text after the quoted separator becomes a separate segment
   that is not a lock run, and the guard refuses a command that does take the lock. The refusal message then
   tells the session to use the lock it is already using. Workaround: avoid separators inside the quoted
   arguments of a locked run (for example `cd` first, outside the lock).
3. **PRE-EXISTING bypasses still open (9).** The part of a run segment after the script is never checked, so
   anything that hides a second command inside that segment gets through: a single `&` (A6, A16), command
   substitution `$(...)` or backticks (A14, A15, P6), and any path ending in `ndc-test-lock.sh` counts as the
   lock script, real or not (A8, A9, P7). None is new, and the hook is a guard against mistakes, not against a
   determined session, but `fix.md` does not list them.

## Gate

Passes only if every result passes, the reproduction tests are unchanged, and no NEW bypass exists.

- Reproduction tests unchanged: yes.
- No NEW bypass: yes.
- Every result passes: **no**: check 4a fails (a NEW false refusal of legitimate locked runs), and check 6 is
  partial (`fix.md` understates the quoting limit and omits the pre-existing bypasses).

**Gate: FAIL.**

## Verdict

**FAIL (not accepted as is).** The fix resolves the reported bug (mentions no longer refused from PowerShell,
and a mention no longer excuses an unlocked command), passes the full suite (1409/1409) and introduces no new
bypass. It does introduce a new refusal of correct locked runs whose quoted arguments contain a separator, and
its known-limits section does not describe that. A quote-aware split (or not splitting inside quotes) would
address finding 2; finding 3 is pre-existing and is Nigel's call.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify | 2026-11-18 12:18:40 | 2026-11-18 12:23:19 | Full suite 12:19:29.400 to 12:21:46.469 in `C:\Users\nigel\.claude\logs\git-22.log`; harness and pre-fix guard in the session scratchpad (`harness.js`, `extra.js`, `extra2.js`, `guard-old.js`, `results.json`). |
