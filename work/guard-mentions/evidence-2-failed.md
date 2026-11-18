# guard-mentions: verify evidence (attempt 2)

Pipeline: `bug` (method kit v0), stage **verify, attempt 2**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Verifier: a fresh sub-agent started by the implementer (partial independence: same machine, same parent session;
it did not write the fix, and read `fix.md` only after checks 1 to 5).

Commits checked: `f3418f6` (failing tests), `7036ae2` (pass 1), `de8619a` (pass 2). HEAD at verification: `de8619a`.

## Method

- The guard was driven directly: `node <guard>` with JSON built in a node harness (`String.raw` and template
  strings, never shell text) and `THESEUS_LOCKS_URL=http://localhost:1/locks`. "deny" means stdout contains
  `"permissionDecision":"deny"`.
- Three copies were compared: **now** (`de8619a`), **pass 1** (`git show 7036ae2:...`), **pre-fix**
  (`git show f3418f6:...`).
- Every command the current guard allows but should refuse was then **run in the real shell** (Git Bash, or
  Windows PowerShell 5.1 with `-File`), with the lock-script call replaced by `echo LOCKSEG` /
  `Write-Output LOCKSEG` and the unlocked command replaced by a marker that prints `UNLOCKED-RAN` on its own line
  only if it executes as its own statement. That separates real bypasses from text the shell would not run.
- Classes, as set by the brief: **OK**; **NEW bypass** = allowed now, refused by pass 1 or by the pre-fix guard;
  **PRE-EXISTING bypass** = allowed by the pre-fix guard and pass 1 too; **NEW false refusal** = refused now,
  allowed by the pre-fix guard; **PRE-EXISTING false refusal** = refused by the pre-fix guard too.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Full suite `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"` | PASS | 1421 total, 1421 passed, 0 failed, 0 skipped, exit 0, "Total time: 1.5237 Minutes". No build error, no rerun. Logged line by line to `C:\Users\nigel\.claude\logs\git-22.log` under `<guard-mentions-verify-2>`, banner 12:47:51.384, last line 12:50:16.058. |
| 2 | Reproduction tests unchanged | PASS | `git diff f3418f6 HEAD -- tests/Theseus.Tests/Locks/LockGuardTests.cs` is empty (0 bytes). |
| 3 | Regressions: locked runs and harmless mentions allowed | **FAIL** | 31 locked runs (11 from attempt 1, 20 new) and 27 mentions (14 from attempt 1, 13 new). All 11 attempt-1 locked runs and F13 now allowed (pass 2 fixed them). **2 NEW false refusals (M4, M5)**: a locked run with a quoted, multi-word `VAR=` prefix, `NDC_TEST_LOCK_REASON="two words" bash ~/.claude/bin/ndc-test-lock.sh run ...`. The pre-fix guard allowed them; pass 1 and pass 2 refuse them, because `programAndArgument` strips `VAR=\S*`, so the program becomes `words"`. CLAUDE.md tells sessions to set `NDC_TEST_LOCK_REASON="..."`, so this is a realistic form. 1 PRE-EXISTING false refusal (H9, a PowerShell here-string commit message containing an apostrophe and a line starting `bash ndc-test-lock.sh`). |
| 4 | Adversarial, quote handling | **FAIL** | 59 commands expected to be refused (33 from attempt 1, 26 new X1 to X26) plus 3 controls. **12 NEW bypasses, each confirmed in the real shell** (the unlocked command executed): X1, X2, X3, X4, X8, X19, X21, X23 (Bash) and X9, X10, X20, X22 (PowerShell). Pass 1 refused all 12; the pre-fix guard allowed all 12. Mechanism: a quote character that the shell does not treat as opening a quote (`\"` or `\'` outside quotes in bash, a quote in a bash `#` comment, bash `$'it\'s'`, PowerShell `` `" ``, PowerShell `"x\"` where `\` is not an escape) makes `splitOutsideQuotes` think a quote is open, so the following `;` or newline is not split, the unlocked command stays inside the lock-run segment, and lock-run segments are never checked. X20 also hides a WSL `bash` lock run behind a Git Bash one. X24 and X25 (unterminated quotes) are allowed by the guard but the shell refuses to run them (syntax error), so not bypasses. 8 PRE-EXISTING bypasses from attempt 1 remain (A6, A8, A9, A14, A15, A16, P6, P7). |
| 5 | Guard rails | PASS | WSL `bash` lock run from PowerShell refused (G1, G13, P8, P9); Git Bash allowed (G2, G10 `usr\bin`, L6, M12 single-quoted path); `Theseus.slnx` exempt (G3, G4); `NDC_TEST_LOCK_BYPASS=1` honoured (G5, G12); compose build and `up --build` refused (G6, G7); plain `dotnet test X.sln` refused from both tools (G8, G11); unit suite allowed (G9). |
| 6 | `fix.md` claims match | **PARTIAL** | Pass-2 section is accurate about what it fixed (L4, L5, L10, L11, F13 now allowed; tests unchanged; new test file). But: (a) "Known limits" says "Quote handling is bash-like. PowerShell's backtick escape is not treated as an escape" without saying that this **lets unlocked commands through** (X10, X20, X22), and it is not fully bash-like either: `\"`/`\'` outside quotes, `#` comments and `$'...'` are not handled (X1 to X4, X8, X19, X21, X23), and PowerShell `"x\"` is treated as a bash escape (X9). (b) Its list of "bypasses that remain, all present before this fix" omits these 12, which pass 2 introduced relative to pass 1. (c) It does not mention the quoted `VAR="..."` prefix false refusal (M4, M5), introduced in pass 1 and missed by attempt 1. (d) The "Change" section still says the command "is split ... at newlines, `&&`, `||`, `;` and `|`" without "outside quotes" (corrected only in the pass-2 section). The remaining-bypass list it does give (single `&`, `$(...)`/backticks, any path ending in `ndc-test-lock.sh`) matches what I found. |

## Commands

`L` = `~/.claude/bin/ndc-test-lock.sh`; `GB` = `& "C:\Program Files\Git\bin\bash.exe" "$HOME/.claude/bin/ndc-test-lock.sh"`;
`T` = `dotnet test X.sln`; `\n` / `\r\n` are real line breaks. "Shell" is the real-shell check: **ran** = the
unlocked command executed. Full data: `results.json` and `semantics.json` in
`C:\Users\nigel\AppData\Local\Temp\claude\3c9e613a9d1facbc\233623bc-2f72-4934-ba2b-c4a52669d270\scratchpad\verify2\`.

### Adversarial (expected: deny)

| Id | Tool | Command | Now | Pass 1 | Pre-fix | Shell | Class |
|---|---|---|---|---|---|---|---|
| A1 | Bash | `echo L && T` | deny | deny | allow | | OK |
| A2 | Bash | `echo L; T` | deny | deny | allow | | OK |
| A3 | Bash | `echo L \|\| T` | deny | deny | allow | | OK |
| A4 | Bash | `echo L \| T` | deny | deny | allow | | OK |
| A5 | Bash | `echo L\nT` | deny | deny | allow | | OK |
| A6 | Bash | `bash L status & T` | allow | allow | allow | ran | PRE-EXISTING bypass |
| A7 | Bash | `bash ndc-test-lock.sh; T` | deny | deny | allow | | OK |
| A8 | Bash | `bash /tmp/ndc-test-lock.sh run a b -- T` | allow | allow | allow | (fake script) | PRE-EXISTING bypass |
| A9 | Bash | `sh ./ndc-test-lock.sh run a b -- dotnet build X.sln` | allow | allow | allow | (fake script) | PRE-EXISTING bypass |
| A10 | Bash | `bash -c "L status; T"` | deny | deny | allow | | OK |
| A11 | Bash | `grep ndc-test-lock.sh README.md && dotnet build X.sln` | deny | deny | allow | | OK |
| A12 | Bash | `FOO=1 bash L status && docker compose build api` | deny | deny | allow | | OK |
| A13 | Bash | `cat ndc-test-lock.sh; dotnet test Foo.PersistenceTests` | deny | deny | allow | | OK |
| A14 | Bash | `bash L status $(T)` | allow | allow | allow | (runs, as substitution) | PRE-EXISTING bypass |
| A15 | Bash | ``bash L status `T` `` | allow | allow | allow | (runs, as substitution) | PRE-EXISTING bypass |
| A16 | Bash | `bash L status > /dev/null & docker compose up -d --build api` | allow | allow | allow | ran | PRE-EXISTING bypass |
| A17 | Bash | `"ndc-test-lock.sh" status && dotnet test X.Api.Tests` | deny | deny | allow | | OK |
| A18 | Bash | `./ndc-test-lock.sh status\r\nT` | deny | deny | allow | | OK |
| A19 | Bash | `node ndc-test-lock.sh && T` | deny | deny | allow | | OK |
| A20 | Bash | `bash L run a b -- echo hi; NDC=1 T` | deny | deny | allow | | OK |
| A21 | Bash | `bash L run s s -- dotnet build X.sln && T` | deny | deny | allow | | OK |
| P1 | PowerShell | `Write-Output "ndc-test-lock.sh"; T` | deny | deny | deny | | OK |
| P2 | PowerShell | `GB status; T` | deny | deny | allow | | OK |
| P3 | PowerShell | `cmd /c "bash L status & T"` | deny | deny | deny | | OK |
| P4 | PowerShell | `Start-Process bash -ArgumentList "L run a b -- T"` | deny | deny | deny | | OK |
| P5 | PowerShell | `wsl L run a b -- T` | deny | deny | deny | | OK |
| P6 | PowerShell | `GB status $(T)` | allow | allow | allow | ran | PRE-EXISTING bypass |
| P7 | PowerShell | `& "C:\fake\Git\bin\bash.exe" "C:\fake\ndc-test-lock.sh" run a b -- T` | allow | allow | allow | (fake script) | PRE-EXISTING bypass |
| P8 | PowerShell | `& bash L run a b -- T` | deny | deny | deny | | OK |
| P9 | PowerShell | `bash.exe L run a b -- dotnet build X.sln` | deny | deny | deny | | OK |
| P10 | PowerShell | `Select-String ndc-test-lock.sh x.md\ndocker compose build api` | deny | deny | deny | | OK |
| P11 | PowerShell | `GB status && T` | deny | deny | allow | | OK |
| P12 | PowerShell | `bash -c "L run a b -- T"` | deny | deny | deny | | OK |
| X1 | Bash | `bash L run a b -- echo \" ; T ; echo \"` | **allow** | deny | allow | ran | **NEW bypass** |
| X2 | Bash | `bash L run a b -- echo it\'s; T` | **allow** | deny | allow | ran | **NEW bypass** |
| X3 | Bash | `bash L run a b -- true # "\nT` | **allow** | deny | allow | ran | **NEW bypass** |
| X4 | Bash | `bash L run a b -- true # it's\nT` | **allow** | deny | allow | ran | **NEW bypass** |
| X5 | Bash | `bash L run a b -- echo "x"y"z" ; T` | deny | deny | allow | | OK |
| X6 | Bash | `bash L run a b -- echo 'a\'; T` | deny | deny | allow | | OK |
| X7 | Bash | `bash L run a b -- echo "a\\"; T` | deny | deny | allow | | OK |
| X8 | Bash | `bash L run a b -- echo $'it\'s' ; T` | **allow** | deny | allow | ran | **NEW bypass** |
| X9 | PowerShell | `GB run a b -- echo "x\" ; T ; "y"` | **allow** | deny | allow | ran | **NEW bypass** |
| X10 | PowerShell | ``GB run a b -- echo `" ; T`` | **allow** | deny | allow | ran | **NEW bypass** |
| X11 | PowerShell | `GB run a b -- echo 'it''s' ; T` | deny | deny | allow | | OK |
| X12 | PowerShell | `GB run a b -- @"\nx\n"@\nT` | deny | deny | allow | | OK |
| X13 | PowerShell | `GB run a b -- @"\nhe said "hi\n"@\nT` | deny | deny | allow | | OK |
| X14 | Bash | `bash L run a b -- cat <<EOF\nit's\nEOF\nT` | deny | deny | allow | | OK |
| X15 | Bash | `bash L run a b -- echo "line1\nline2"\nT` | deny | deny | allow | | OK |
| X16 | Bash | `echo "; bash L run a b -- " ; T` | deny | deny | allow | | OK |
| X17 | Bash | `bash L run a b -- echo "a" 'b"' ; T` | deny | deny | allow | | OK |
| X18 | PowerShell | `GB run a b -- echo 'x"' ; T` | deny | deny | allow | | OK |
| X19 | Bash | `bash L run a b -- echo \' ; T ; echo \'` | **allow** | deny | allow | ran | **NEW bypass** |
| X20 | PowerShell | ``GB status `" ; bash L run a b -- T`` (WSL lock run hidden) | **allow** | deny | allow | ran | **NEW bypass** |
| X21 | Bash | `bash L run a b -- echo "a" \"\ndocker compose build api` | **allow** | deny | allow | ran | **NEW bypass** |
| X22 | PowerShell | ``GB run a b -- Write-Output "a`"b" ; T`` | **allow** | deny | allow | ran | **NEW bypass** |
| X23 | Bash | `"L" run a b -- echo \"; T` | **allow** | deny | allow | ran | **NEW bypass** |
| X24 | Bash | `bash L run a b -- echo "unterminated\nT` | allow | deny | allow | not run (bash: unexpected EOF) | OK (not executable) |
| X25 | PowerShell | `GB run a b -- echo 'x\nT` | allow | deny | allow | not run (parse error) | OK (not executable) |
| X26 | Bash | `bash L run "a\" b" c -- x; T` | deny | deny | allow | | OK |

### Controls (quoted text is harmless; expected: allow)

| Id | Tool | Command | Now | Pass 1 | Pre-fix | Class |
|---|---|---|---|---|---|---|
| C1 | Bash | `bash L run a b -- echo "a; T"` | allow | deny | allow | OK |
| C2 | Bash | `bash L run a b -- x"; T; echo "` | allow | deny | allow | OK |
| C3 | Bash | `bash L run a b -- echo \\"; T; echo "` | allow | deny | allow | OK |

### Harmless mentions (expected: allow)

| Id | Tool | Command | Now | Pass 1 | Pre-fix | Class |
|---|---|---|---|---|---|---|
| F1 | Bash | `git commit -m "guard: treat only runs of ndc-test-lock.sh as locked"` | allow | allow | allow | OK |
| F2 | Bash | `grep -n ndc-test-lock.sh tools/locks/*.js` | allow | allow | allow | OK |
| F3 | Bash | `git log --oneline -- tools/locks/ndc-test-lock.sh` | allow | allow | allow | OK |
| F4 | Bash | `cat ~/.claude/bin/ndc-test-lock.sh \| head -20` | allow | allow | allow | OK |
| F5 | PowerShell | `git commit -m "guard: treat only runs of ndc-test-lock.sh as locked"` | allow | allow | deny | OK |
| F6 | PowerShell | `Select-String -Path tools\locks\*.js -Pattern 'ndc-test-lock.sh'` | allow | allow | deny | OK |
| F7 | PowerShell | `Get-Content "$HOME\.claude\bin\ndc-test-lock.sh" -TotalCount 20` | allow | allow | deny | OK |
| F8 | PowerShell | `git show HEAD -- tools/locks/ndc-test-lock.sh` | allow | allow | deny | OK |
| F9 | PowerShell | `git diff HEAD~1 -- tools/locks/ndc-test-lock.sh \| Select-String run` | allow | allow | deny | OK |
| F10 | PowerShell | `rg "ndc-test-lock\.sh" tools` | allow | allow | allow | OK |
| F11 | PowerShell | `Get-ChildItem ~/.claude/bin \| Where-Object Name -eq ndc-test-lock.sh` | allow | allow | deny | OK |
| F12 | Bash | `git diff HEAD~1 -- tools/locks/ndc-test-lock.sh` | allow | allow | allow | OK |
| F13 | PowerShell | `git commit -m "docs; bash ndc-test-lock.sh run is the form"` | allow | deny | deny | OK (fixed in pass 2) |
| F14 | Bash | `git commit -m "docs; bash ndc-test-lock.sh run is the form"` | allow | allow | allow | OK |
| H1 | PowerShell | `git commit -m "fix: only runs of ndc-test-lock.sh; mentions are fine"` | allow | allow | deny | OK |
| H2 | PowerShell | `git log --grep "bash ndc-test-lock.sh && more"` | allow | allow | deny | OK |
| H3 | Bash | `echo 'bash ndc-test-lock.sh run a b -- x'` | allow | allow | allow | OK |
| H4 | PowerShell | `Write-Output 'bash ndc-test-lock.sh run "a" "b" -- dotnet test X.Unit.Tests'` | allow | allow | deny | OK |
| H5 | PowerShell | `code tools/locks/ndc-test-lock.sh` | allow | allow | deny | OK |
| H6 | PowerShell | `notepad "$HOME\.claude\bin\ndc-test-lock.sh"` | allow | allow | deny | OK |
| H7 | Bash | `wc -l ~/.claude/bin/ndc-test-lock.sh tools/locks/ndc-test-lock-guard.js` | allow | allow | allow | OK |
| H8 | PowerShell | `git commit -m @'\nguard: bash ndc-test-lock.sh run; mention\n'@` | allow | allow | deny | OK |
| H9 | PowerShell | `git commit -m @'\nit's about the guard\nbash ndc-test-lock.sh run is the form\n'@` | **deny** | deny | deny | PRE-EXISTING false refusal |
| H10 | Bash | `git commit -m "$(cat <<'EOF'\nguard: bash ndc-test-lock.sh run; it's fine\nEOF\n)"` | allow | allow | allow | OK |
| H11 | PowerShell | `Select-String -Pattern "ndc-test-lock.sh run" -Path x \| Select-Object -First 5` | allow | allow | deny | OK |
| H12 | PowerShell | `git commit -m "guard: it's only runs of ndc-test-lock.sh"` | allow | allow | deny | OK |
| H13 | Bash | `git commit -m 'guard: bash ndc-test-lock.sh run; mention'` | allow | allow | allow | OK |

### Locked runs (expected: allow)

| Id | Tool | Command | Now | Pass 1 | Pre-fix | Class |
|---|---|---|---|---|---|---|
| L1 | Bash | `bash L run "s" "suite" -- T --no-build` | allow | allow | allow | OK |
| L2 | Bash | `NDC_TEST_LOCK_WAIT=600 bash L run "s" "suite" -- dotnet test X.Api.Tests` | allow | allow | allow | OK |
| L3 | Bash | `cd /e/x && bash L run "s" "suite" -- T` | allow | allow | allow | OK |
| L4 | Bash | `bash L run "s" "suite" -- bash -c "cd x && T"` | allow | deny | allow | OK (fixed in pass 2) |
| L5 | Bash | `bash L run "s" "a;b" -- T` | allow | deny | allow | OK (fixed in pass 2) |
| L6 | PowerShell | `GB run "s" "suite" -- T` | allow | allow | allow | OK |
| L7 | Bash | `bash L run --token abc -- T` | allow | allow | allow | OK |
| L8 | Bash | `bash L run "s" "suite" -- T --filter "FullyQualifiedName~A\|FullyQualifiedName~B"` | allow | allow | allow | OK |
| L9 | PowerShell | `$env:NDC_TEST_LOCK_REASON='x'; GB run "s" "suite" -- T` | allow | allow | allow | OK |
| L10 | Bash | `bash L run "s" "suite" -- sh -c 'dotnet build X.sln; T --no-build'` | allow | deny | allow | OK (fixed in pass 2) |
| L11 | PowerShell | `GB run "s" "suite" -- bash -c "cd /e/x && T"` | allow | deny | allow | OK (fixed in pass 2) |
| M1 | Bash | `bash L run "s" "suite; with semi" -- dotnet test X.Api.Tests` | allow | deny | allow | OK |
| M2 | Bash | `bash L run 's' 'a\|b' -- T` | allow | deny | allow | OK |
| M3 | Bash | `cd "/e/my dir" && bash L run "s" "suite" -- dotnet test X.PersistenceTests --filter "A\|B"` | allow | allow | allow | OK |
| M4 | Bash | `NDC_TEST_LOCK_REASON="fix && verify" bash L run "s" "suite" -- T` | **deny** | deny | allow | **NEW false refusal** |
| M5 | Bash | `NDC_TEST_LOCK_REASON="two words" bash L run "s" "suite" -- T` | **deny** | deny | allow | **NEW false refusal** |
| M6 | Bash | `NDC_TEST_LOCK_WAIT=600 NDC_TEST_LOCK_REASON=x bash L run "s" "suite" -- T` | allow | allow | allow | OK |
| M7 | Bash | `bash L run "s" "suite" -- T --logger "console;verbosity=detailed"` | allow | allow | allow | OK |
| M8 | Bash | `bash L run "s" "suite" -- T 2>&1 \| tee -a ~/.claude/logs/x.log` | allow | allow | allow | OK |
| M9 | Bash | `bash L run --token t -- docker compose build api` | allow | allow | allow | OK |
| M10 | PowerShell | `GB run "s" "suite" -- T --logger "console;verbosity=detailed"` | allow | allow | allow | OK |
| M11 | PowerShell | `GB run "s" "it's a suite" -- T` | allow | allow | allow | OK |
| M12 | PowerShell | `& 'C:\Program Files\Git\bin\bash.exe' "$HOME/.claude/bin/ndc-test-lock.sh" run "s" "suite" -- T` | allow | allow | allow | OK |
| M13 | PowerShell | `$env:BYM_NUGET_PASSWORD = "$(az keyvault secret show ... -o tsv)"; GB run "s" "suite" -- T` | allow | allow | allow | OK |
| M14 | Bash | `export BYM_NUGET_PASSWORD="$(az keyvault secret show ... -o tsv \| tr -d '\r\n')" && bash L run "s" "suite" -- T` | allow | allow | allow | OK |
| M15 | Bash | `bash L run "s" "suite \"quoted\"" -- T` | allow | allow | allow | OK |
| M16 | Bash | `bash "$HOME/.claude/bin/ndc-test-lock.sh" run "s" "suite" -- T` | allow | allow | allow | OK |
| M17 | Bash | `bash L run "s" "suite" -- docker compose up -d --build api` | allow | allow | allow | OK |
| M18 | PowerShell | `GB run "s" "suite" -- bash -c 'cd /e/x; T'` | allow | deny | allow | OK |
| M19 | PowerShell | ``GB run "s" "say `"hi`"" -- T`` | allow | allow | allow | OK |
| M20 | Bash | `bash L run "s" "it's" -- T` | allow | allow | allow | OK |

### Guard rails

| Id | Tool | Command | Expected | Now | Pass 1 | Pre-fix | Class |
|---|---|---|---|---|---|---|---|
| G1 | PowerShell | `bash "$HOME/.claude/bin/ndc-test-lock.sh" run "s" "suite" -- T` | deny | deny | deny | deny | OK |
| G2 | PowerShell | `GB status` | allow | allow | allow | allow | OK |
| G3 | Bash | `dotnet test Theseus.slnx --nologo` | allow | allow | allow | allow | OK |
| G4 | PowerShell | `dotnet build Theseus.slnx` | allow | allow | allow | allow | OK |
| G5 | Bash | `NDC_TEST_LOCK_BYPASS=1 T` | allow | allow | allow | allow | OK |
| G6 | Bash | `docker compose build api` | deny | deny | deny | deny | OK |
| G7 | PowerShell | `docker compose up -d --build api` | deny | deny | deny | deny | OK |
| G8 | Bash | `T` | deny | deny | deny | deny | OK |
| G9 | Bash | `dotnet test Foo.UnitTests` | allow | allow | allow | allow | OK |
| G10 | PowerShell | `& "C:\Program Files\Git\usr\bin\bash.exe" ~/.claude/bin/ndc-test-lock.sh status` | allow | allow | allow | allow | OK |
| G11 | PowerShell | `T` | deny | deny | deny | deny | OK |
| G12 | PowerShell | `$env:NDC_TEST_LOCK_BYPASS=1; T` | allow | allow | allow | allow | OK |
| G13 | PowerShell | `bash L status` | deny | deny | deny | deny | OK |

## Findings

1. **12 NEW bypasses (X1, X2, X3, X4, X8, X9, X10, X19, X20, X21, X22, X23), each confirmed to run the unlocked
   command in the real shell.** `splitOutsideQuotes` opens a quote on any `"` or `'` outside quotes, so a quote
   character the shell does not treat as a quote (escaped `\"`/`\'`, inside a `#` comment, inside `$'...'`,
   PowerShell `` `" ``) or a PowerShell `"x\"` (where `\` is not an escape) keeps the next separator from
   splitting. The unlocked command then sits inside the lock-run segment, which is never checked. Pass 1 refused
   all 12. The pre-fix guard allowed all 12 too (it allowed anything mentioning the script from Bash), so these
   are not worse than the code before the bug fix, but they are worse than pass 1 and they are what the new
   quote handling adds. They need a deliberate shell-quoting trick; none is a likely accident.
2. **2 NEW false refusals (M4, M5)**, present since pass 1 and missed by attempt 1: a quoted multi-word
   `VAR="..."` prefix before the lock run (`NDC_TEST_LOCK_REASON="two words" bash L run ...`). `programAndArgument`
   strips `VAR=\S*`, which stops at the space inside the quotes. The pre-fix guard allowed these.
3. Pass 2 fixed everything attempt 1 failed it on: L4, L5, L10, L11 and F13 are allowed, and C1 to C3, M1, M2,
   M18 (separators inside a locked run's quoted arguments) are allowed.
4. 8 PRE-EXISTING bypasses remain as `fix.md` lists them (single `&`, `$(...)`/backticks, any path ending in
   `ndc-test-lock.sh`); 1 PRE-EXISTING false refusal (H9).

## Gate

Passes only if every check passes, the reproduction tests are unchanged, and there is no NEW bypass and no NEW
false refusal.

- Every check passes: **no** (checks 3 and 4 fail, check 6 partial).
- Reproduction tests unchanged: yes.
- No NEW bypass: **no** (12).
- No NEW false refusal: **no** (2: M4, M5).

**Gate: FAIL.**

## Verdict

**FAIL.** Pass 2 resolves the reported bug and the regressions attempt 1 found, and the full suite passes
(1421/1421). But the quote-aware split adds 12 bypasses that pass 1 refused, by letting a stray or escaped quote
hide a separator inside the unchecked lock-run segment, and a quoted `VAR="two words"` prefix on a locked run is
still refused where the pre-fix guard allowed it. `fix.md` does not describe either. Possible directions, for the
implementer and Nigel to choose: treat a backslash outside quotes and PowerShell's backtick as escapes and stop
quote tracking at `#`; or fall back to refusing (or to splitting naively) when the quote state is ambiguous; and
strip quoted `VAR="..."` values in `programAndArgument`.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify, attempt 2 | 2026-11-18 12:47:08 | 2026-11-18 12:54:27 | Full suite 12:47:51.384 to 12:50:16.058 in `C:\Users\nigel\.claude\logs\git-22.log`. Harness, guard copies (`guard-now.js`, `guard-pass1.js`, `guard-prefix.js`), `harness.js`, `semantics.js`, `results.json`, `semantics.json` and `sem-*.sh`/`sem-*.ps1` in `C:\Users\nigel\AppData\Local\Temp\claude\3c9e613a9d1facbc\233623bc-2f72-4934-ba2b-c4a52669d270\scratchpad\verify2\`. |
