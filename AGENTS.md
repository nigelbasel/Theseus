# AGENTS.md

The instructions for any coding agent working in this repository, whatever its vendor. `CLAUDE.md` points
here. Keep this file a map: short, current, and specific to this repo.

## What Theseus is

A Windows desktop app that supervises interactive Claude Code sessions ("workstreams"). It launches them
into Windows Terminal tabs, resumes them tomorrow, stops them with a handover, tracks their time, links
them into chains, and holds the machine's shared locks. It is growing into a method layer for agentic
development; the method kit is in [`method/`](method/README.md).

## Where things live

| Path | What it is | Rule |
|---|---|---|
| `src/Theseus.Core` | All behaviour: domain, launching, stopping, links, proposals, locks, MCP server, attention, reporting, persistence. Targets `net11.0` with no WPF | Logic goes here, so it can be tested without a window |
| `src/Theseus.App` | The WPF Desk: windows, XAML, view models, services that wire Core to Windows | Thin. Views bind to view models; view models call Core |
| `src/Theseus.Launcher` | The shim a terminal tab runs: collects its launch spec over a named pipe and starts Claude Code | Nothing in a launch spec ever reaches a command line the terminal parses |
| `src/Theseus.Hook` | The hook a session runs to learn Theseus has stopped or restarted | Must work while Theseus is down |
| `tests/Theseus.Tests` | NUnit tests for all of the above | One test project |
| `tools/locks` | `ndc-test-lock.sh`, the wrapper script that asks Theseus for a lock | Tested from `tests/Theseus.Tests/Locks` |
| `docs/` | The user help, published as the GitHub Pages site | User-facing only. Nothing internal, no work artefacts |
| `method/` | Method kit v0, generic: pipelines, project format, rules, and `starter/`, the files a new project begins with | Changes to it are reviewed like code. Nothing Theseus-specific |
| `.agents/` | This repo's own method files: the reviewer rubric | |
| `installer/`, `scripts/Build-Installer.ps1` | The WiX v6 MSI | CI builds it the same way |

## Build and test

```
dotnet build Theseus.slnx
dotnet test Theseus.slnx
```

- `global.json` pins the SDK (a .NET 11 release candidate). Do not change it to make a build pass.
- `Directory.Build.props` sets `TreatWarningsAsErrors`. A warning is a failure; fix it, do not suppress it.
- The tests start no containers and need no lock.
- CI (`.github/workflows/ci.yml`) runs the tests on `windows-latest` and publishes a release, with the MSI,
  for every push to `main` that passes.

## Conventions

- **One type per file**, the file named after the type. Private nested types stay with their parent.
- **XML doc comments on types and members say why**, and cite the decision they come from, dated:
  "(Nigel, 2026-10-06)" or "(decision 10, 2026-10-21)". Inline `//` comments are allowed where the code
  cannot say why on its own.
- `var` is used. Braces on every control structure.
- **Tests are named as sentences**: `Subject_Behaviour_Qualification`, for example
  `TheFirstReading_HasNoRates_ButCountsProcessesAndMemory`. A test proves one behaviour.
- **Write the test first** for a bug: a failing test that reproduces it, then the fix.
- Commit on `main` unless told otherwise. **Push only when the owner says so.**

## Invariants

These are the decisions most often at risk. The full list, with sources, is the reviewer rubric:
[`.agents/reviewer-rubric.md`](.agents/reviewer-rubric.md). It is this repo's own: every item cites a Theseus decision,
so it is not part of the generic kit in `method/`.

- Anything that changes the board (a link, a proposal, an approval, a report) passes the person in the tab
  first. A report is a claim, never a fact, until confirmed on the board.
- Nothing starts a session on its own. Ready is shown, never acted on. The one exception (Nigel, 2026-11-20): inside
  a project at autonomy level L1 or above, Theseus starts and stops that project's workstreams for objectives already
  in its approved project file. Until the levels are built (phase 2), there is no such project. Duties are named
  exceptions too (Nigel, 2026-11-24): a duty the owner set is his instruction, and one a session set is a start that
  session could make with start_workstream, deferred; the overnight queue, which acts on nobody's request, is off until
  the owner switches it on for a project, and only ever wakes an open, idle card.
- Theseus reads trackers and never writes to them. Sessions never report durations.
- Only the owner reorders, cancels or releases a lock, from the board. Clean-up touches only what can be
  proved to belong to the run being cleaned up.
- Nothing destructive happens at startup without asking.
- No secrets on the machine: nothing needs a token to run or update.

## The repo is public

- No screenshots of a real board, no secrets, no names of employers' internal systems, repos, issue keys or
  machine paths. Examples use placeholders such as `<root>\<feature>\<repo>`.
- Independent holdout scenarios judge changes to Theseus. They are kept outside this repo on purpose.
  Do not look for them; if you think one is needed or wrong, say so to the owner. The one exception is a verifier
  card (its session runs with `THESEUS_ROLE=verifier`): it reads them as its brief says, and writes only in the
  holdout's results folder.

## Working through the method

Ad hoc work needs nothing from the method: it runs as today (the `freeform` pipeline). Work in a project
follows its pipeline, writes its artefacts under `work/<slug>/` in the project's **home folder**, outside this repo
(the folder Theseus's Settings names as the projects folder, with one subfolder per project), and is reviewed
against the rubric. This repo holds code and the method kit only. See [`method/README.md`](method/README.md).
