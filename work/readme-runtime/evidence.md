# readme-runtime: evidence

Pipeline: `bug` (method kit v0), stage **verify**, autonomy L0. Branch `main`, repo Theseus.
Verifier: a sub-agent session started by the implementer (partial independence: separate context, same
model and same machine; it formed its view from `git show 1f1d73c`, `git show 5a0e0ce` and the files, not
from `fix.md`).

Commits verified: `1f1d73c` (failing test) and `5a0e0ce` (README fix). HEAD is `5a0e0ce`; `main` is 5
commits ahead of `origin/main`, so CI has not run on this change.

## Checks

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | The download page offers a .NET 11 Desktop Runtime for Windows | **PASS** | WebFetch of https://dotnet.microsoft.com/download/dotnet/11.0 at about 11:48: lists ".NET Desktop Runtime 11.0.0", released November 10, 2026, labelled "Security patch" with no Preview or Go-live label, with Windows x64, x86 and Arm64 installers (`/en-us/download/dotnet/thank-you/runtime-desktop-11.0.0-windows-x64-installer` and so on). |
| 2 | An app built with the pinned RC SDK, framework-dependent, targeting `net11.0-windows`, starts on that runtime | **PASS** | **Rule:** https://github.com/dotnet/runtime/blob/main/docs/design/features/framework-version-resolution.md, "3.0 and forward": "When resolving framework reference with a pre-release version, treat all versions the same ... pre-release can resolve to both release or pre-release". Its table gives `2.1.0-preview.1` with `2.1.0` available resolving to `2.1.0`. The default `RollForward` is `Minor` (https://learn.microsoft.com/en-us/dotnet/core/versions/selection, Values table). No project sets `RollForward` or `RuntimeFrameworkVersion` (grep of `*.csproj`/`*.props`: none). **Observed:** I copied `src/Theseus.Launcher/bin/Debug/net11.0` to the scratchpad, rewrote its runtimeconfig to request `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App` `11.0.0-rc.2.26504.105` (the version an RC SDK would write), and ran it with `COREHOST_TRACE=1`. The host traced `Attempting FX roll forward starting from version='[11.0.0-rc.2.26504.105]' ... version_compatibility_range=minor` and then `Chose FX version [C:\Program Files\dotnet\shared\Microsoft.NETCore.App\11.0.0]` and `Chose FX version [...\Microsoft.WindowsDesktop.App\11.0.0]`. The app ran and printed its usage line (exit 2, the expected code with no arguments). No RC `Microsoft.NETCore.App` is installed here, so that half is the clean-machine case. A GA SDK build writes `11.0.0` (checked: `src/Theseus.App/bin/Debug/net11.0-windows/Theseus.runtimeconfig.json`), which 11.0.0 satisfies trivially. **Caveat:** I did not inspect the runtimeconfig in a CI-built MSI. CI installs the SDK from `global.json` (`.github/workflows/ci.yml:33-35`, `setup-dotnet` with `global-json-file`). Either value it could write resolves to 11.0.0, as shown above. |
| 3 | The full suite passes | **PASS** (second run) | `dotnet test Theseus.slnx --nologo --logger "console;verbosity=normal"`, every line logged to the workstream log under `<readme-runtime-verify>`. **Run 1** (banner 11:48:09.925) failed during the build, not in a test: `CSC : error CS0009: Metadata file '...\src\Theseus.Launcher\obj\Debug\net11.0\ref\Theseus.Launcher.dll' could not be opened -- PE image doesn't contain managed metadata` (11:48:20.341, exit 1). The ref assembly was being rewritten during that same build (mtime 11:48), so this looks like a transient incremental-build race. Nothing in the change touches the build. **Run 2** (banner 11:48:52.456): `Test Run Successful. Total tests: 1401, Passed: 1401` (failed 0), exit 0 at 11:50:32.965. `TheRuntimeTheReadmeAsksFor_IsTheOneTheAppTargets` passed at 11:50:27.745. This run used SDK `11.0.100` (GA, picked by `rollForward: latestFeature`), not the pinned RC, because the RC SDK is not installed here (`dotnet --list-sdks`). |
| 4 | The reproduction test is unchanged since it failed | **PASS** | `git diff 1f1d73c HEAD -- tests/Theseus.Tests/Repository/ReadmeTests.cs` printed 0 bytes. So did the same diff against the working tree. |
| 5 | Nothing else in the README or docs is now untrue about the .NET version | **PASS** | `grep -niE 'dotnet\|sdk\|runtime\|\.net'` over `README.md` and `docs/*.md` matches only README.md:27 (the fixed line) and README.md:68-69 (`dotnet build` / `dotnet test`, no version). `docs/` mentions no .NET version. `AGENTS.md:17` (`net11.0`) and `:37` (".NET 11 release candidate" SDK) are consistent. All TFMs are `net11.0` (3 projects) or `net11.0-windows` (2). One gap, not an untruth: README "Building" does not say which SDK is needed. A builder needs a .NET 11 SDK, and `global.json` enforces that. |
| 6 | The test is sound | **PASS**, with limits noted | It cannot pass vacuously. `targeted` is asserted non-empty, and `named` and `linked` must equal it, so a regex that matches nothing in the README gives `""` and fails. If the App project changes form (for example `TargetFrameworks` plural, or `net11.1-windows`), `targeted` is empty and the test fails loudly rather than passing. A framework bump without a README edit fails, as shown by the failure at 1f1d73c ("Expected 11, but was 10"). **Limits:** (a) it checks only the first match of each pattern, so a second, stale ".NET N Desktop Runtime" mention elsewhere in the README would go unnoticed. (b) It reads only `Theseus.App.csproj`, which is the right project, since the MSI ships the App. (c) It finds the repo by `[CallerFilePath]`, which breaks if PathMap/deterministic source paths are ever enabled. There is no PathMap today, and `LockGuardTests`, `LockScriptTests` and `HelpViewModelTests` already rely on the same approach. None of these lets the test pass while the current README is wrong. |

## Gate

Pass only if every repo has a result, every result passes, and the reproduction test is unchanged.

- Repos: one (Theseus). It has a result for all six checks.
- Every result passes. Check 3 passes on the second run, and the first run's build error is recorded above.
- The reproduction test is unchanged (check 4).

**Gate: met.**

## Verdict

**PASS.** The README now names and links the .NET 11 Desktop Runtime. Microsoft offers that runtime for
Windows as 11.0.0 (GA, 2026-11-10). An RC-SDK-built framework-dependent build rolls forward onto it under the
documented rule, and the host trace confirms it. The full suite passes (1401/1401). The test guards the
mismatch without being able to pass vacuously. For Nigel: the transient CS0009 on the first run, and the fact
that CI has not yet run this change (`main` is 5 ahead of `origin/main`).

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| verify | 2026-11-18 11:47:43 | 2026-11-18 11:51:10 | Both times read from `date` in the verifier's shell. The test runs ran from 11:48:09.925 to 11:48:21.061 (build error) and from 11:48:52.456 to 11:50:32.965 (pass), per the log. The finish time is when the checks ended, before this file was written |
