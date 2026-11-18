# guard-mentions: reproduction

Pipeline: `bug` (method kit v0), stage **reproduce**, autonomy L0. Card git-22, branch `main`, repo Theseus.

## The report

From PowerShell, the lock guard (`tools/locks/ndc-test-lock-guard.js`, installed to `~/.claude/bin`) refused a
`git commit` whose message only *mentioned* the lock script. It said WSL's bash would be used, though nothing
was being run with bash at all. Seen on 2026-11-18 in this workstream, while committing the neutral-names change.

## What reading the code found

Line 82 treats any command that **contains** `ndc-test-lock.sh` as a run of the script:

```js
if (/ndc-test-lock\.sh/.test(command)) {
  ... if PowerShell and not Git Bash: deny (the WSL warning)
  announceWaitIfHeldByAnother(...); return;   // every later check is skipped
}
```

That one cause has two symptoms:

1. **False refusal (PowerShell).** A command that only mentions the script is refused with the WSL warning.
2. **Bypass (Bash and PowerShell).** Once the script is mentioned, the guard returns before its container-suite,
   compose and solution-build checks. A command that mentions the script and then runs an unlocked suite is
   allowed. This breaks the lock's purpose (reviewer rubric R5: only the lock decides who runs).

## Expected, and where that comes from

- Only a command that **runs** the lock script is treated as a run of it. Source: the guard's own header
  ("refuses ... runs ... that do not go through ndc-test-lock.sh") and `docs/locks.md`.
- Running it through WSL bash from PowerShell is still refused; through Git Bash it is allowed. Source: the
  machine's standing instructions and the guard's existing message.
- Mentioning the script never exempts anything else in the same command from the guard's other checks.

## The failing tests

`tests/Theseus.Tests/Locks/LockGuardTests.cs`. The helper now takes the tool name (default `Bash`, so the existing
cases are unchanged).

| Test | Cases | Result before the fix |
|---|---|---|
| `APowerShellCommandThatOnlyMentionsTheLockScript_IsAllowed` | git commit message, Select-String, git log | **3 fail**: refused |
| `MentioningTheLockScript_DoesNotExemptAnUnlockedRunInTheSameCommand` | echo + `dotnet test Other.sln`; grep + `dotnet build *.sln` | **2 fail**: allowed with no lock |
| `RunningTheLockScriptThroughWslBashFromPowerShell_IsStillRefused` | `bash ~/...` run, `bash "$HOME/..."` wait | pass (guard rail) |
| `RunningTheLockScriptThroughGitBashFromPowerShell_IsAllowed` | `& "C:\Program Files\Git\bin\bash.exe" ...` | pass (guard rail) |
| existing `TheseusSolution_IsAllowedWithoutTheLock`, `OtherSolutionsAndContainerSuites_AreStillRefused` | 7 | pass (guard rail) |

Run logged at 2026-11-18 12:11:24 under `<guard-mentions>`: 15 total, 10 passed, 5 failed.

## Gate

Evidence: the new tests fail, for the reported reasons, on the unfixed code. **Met**, for both symptoms. The
guard-rail cases pass, so the fix has to keep them passing.

## Note for the fix and verify stages

The installed copy in `~/.claude/bin` is what sessions actually run. A fix in the repo reaches it only through
the installer (`install.ps1` copies both lock files), so the verifier must say which copy it tested.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| reproduce | 2026-11-18 12:10:42 (logged) | this commit's time | Failing run logged at 12:11:24 |
