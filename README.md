# Theseus

A Windows desktop orchestrator for [Claude Code](https://claude.com/claude-code) sessions.

Theseus does not host terminals. It launches Claude Code into Windows Terminal tabs and tracks them
from the outside — so the sessions you already have running, started by hand, appear on the same
board as the ones it started itself.

Named for the thread through the Labyrinth: the point of the app is holding the thread back to
yesterday's work.

## What it does

- **A board** of workstreams — Today, In flight, Parked, Done — with drag between lanes.
- **Start** one workstream, a whole lane, or every lane except the last.
- **Resume** yesterday's session. Theseus mints the session identifier *before* the tab opens, which
  is what makes a session resumable tomorrow rather than merely restartable.
- **Focus** a session's terminal tab, including sessions it did not start.
- **Adopt** a session that is already running into a workstream, or create a workstream from it.
- **Account usage** in the status bar — the 5-hour and 7-day windows, and whichever limit is actually
  binding.
- **Automatic tab colours**, allocated farthest-first in OKLab so no two workstreams look alike.

## Requirements

- Windows 10 1809 or later
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (unless you build a
  self-contained installer)
- Windows Terminal
- Claude Code

## Installing

Download the `.msi` from the releases page, or build and install from source in one step:

```powershell
.\install.ps1
```

That packages what is in the working tree and installs it, asking for elevation only for the install
itself. Sessions running in terminal tabs are holding files the installer replaces, so it warns
before closing them; stop them from Theseus first, or pass `-Force`.

To produce the `.msi` without installing it, add `-BuildOnly`, or call
`.\scripts\Build-Installer.ps1` directly.

For a fast inner loop while developing, copy a build straight into Program Files instead — this one
needs an elevated terminal:

```powershell
.\scripts\Deploy-Theseus.ps1
```

## Building

```powershell
dotnet build
dotnet test
```

## How it works

Claude Code writes a registry entry to `%USERPROFILE%\.claude\sessions\<pid>.json` when a session
starts and deletes it on a clean exit. That file is the contract Theseus is built on: it needs no
hooks, no status line, and no cooperation from the sessions themselves. Everything else — the launch
pipe, the run log — is a bonus signal for sessions Theseus started.

A tab is opened by running `WindowsTerminal.exe new-tab` against a **launcher shim** rather than
Claude Code directly. The shim collects its instructions over a named pipe, so no prompt, path or
title ever travels on a command line — which kills the terminal's quoting rules by construction
rather than by escaping. The shim is also the only process with a real parent/child relationship to
Claude Code, so it can report an exit code.

Identifying which terminal tab holds which session is the one genuinely hard problem. Windows
Terminal exposes no per-tab identifier through UI Automation and reports the same process for every
tab. Theseus reads the title a session gave its own console — by attaching to that console — and
matches it against the tab titles, which works for sessions it did not launch.

## Where it keeps things

| Path | Contents |
|---|---|
| `%LOCALAPPDATA%\Theseus\board.json` | Lane, flags and adopted session per workstream |
| `%LOCALAPPDATA%\Theseus\runs.jsonl` | Every launch attempt, append-only |
| `%LOCALAPPDATA%\Theseus\settings.json` | Paths, terminal window name, theme |
| `%LOCALAPPDATA%\Theseus\logs\` | Daily log, 14 days retained |

Workstreams themselves live as plain JSON plan files in a directory you choose, one per workstream,
hand-editable and diffable. That format is a contract with the Claude Code sessions that also write
them.

## Licence

MIT. See [LICENSE](LICENSE).
