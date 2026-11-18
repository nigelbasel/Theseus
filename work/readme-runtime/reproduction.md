# readme-runtime: reproduction

Pipeline: `bug` (method kit v0), stage **reproduce**, autonomy L0. Card git-22, branch `main`, repo Theseus.

## The report

`README.md` tells a new user to install the **.NET 10 Desktop Runtime**, and links to the .NET 10 download.
Every project targets **.NET 11** (`net11.0` / `net11.0-windows`), and CI builds the published MSI
framework-dependent (`scripts/Build-Installer.ps1` without `-SelfContained`). A user who installs what the
README asks for gets an app that cannot start.

Found while reading the README on 2026-11-18, during stage 0. Not reported by a user.

## Expected, and where that comes from

The README's runtime requirement names, and links to, the major version that `src/Theseus.App/Theseus.App.csproj`
targets. Source: the project files themselves, and the README's own promise ("Requirements").

## Actual

| Where | Says |
|---|---|
| `src/Theseus.App/Theseus.App.csproj` | `<TargetFramework>net11.0-windows</TargetFramework>` |
| `README.md:27` | `[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)` |

## The failing test

`tests/Theseus.Tests/Repository/ReadmeTests.cs`, `TheRuntimeTheReadmeAsksFor_IsTheOneTheAppTargets`. It reads
the App project's target framework and checks that the README names it and links to it. It also keeps the two
in step from now on: the next framework change fails the build until the README follows.

```
Failed TheRuntimeTheReadmeAsksFor_IsTheOneTheAppTargets
  Expected: "11"
  But was:  "10"
```

Run 2026-11-18 11:34:35, logged in the workstream log under `<readme-runtime>`.

## Gate

Evidence: the new test fails, for the reported reason, on the unfixed code. **Met.** It fails on the runtime
the README names, which is the defect.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| reproduce | [not recorded] | 2026-11-18 11:35:06 (commit 1f1d73c) | Failing run logged at 11:34:35. The test is committed before any fix. Neither time first written here (11:27 start, 11:36 finish) was measured; both were estimates and have been replaced by the log and git times |
