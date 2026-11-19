# verifier-card: research

Pipeline: `feature` (method kit v0, P1 to P22 once pull request #2 merges), stage **research**, autonomy L0. Project
`theseus-bootstrap`, phase 1, objective 1.0. Branch `nigel/verifier-card`, repo Theseus.

The objective (from `projects/theseus-bootstrap.md`): when a workstream reaches `verify` and its spec names a holdout
scenario, Theseus launches a fresh verifier session that can read the holdout repository and cannot change the code.
It runs the scenario, asks the owner in its own tab for any step only a person can do, and hands back the scenario
name, pass or fail and a one-line reason through `report_back`, confirmed on the board. The implementing session sees
only that verdict. Its first job: the three holdout scenarios against the stage 0 changes and close-step (pull
request #1, which waits on `stop-with-handoff`).

## What exists today

All references are under `src/` at 1c5d59e.

**Launching a card.**
- Start → `ShellViewModel.StartMany` → `LaunchCoordinator.Launch` (`Theseus.App/Services/LaunchCoordinator.cs:112-199`).
  It builds a `LaunchSpec` and registers a ticket on a named pipe. Windows Terminal then runs the launcher, which redeems
  the ticket and starts `claude` (`Theseus.Launcher/Program.cs:13-23`, `ClaudeProcessRunner.cs:61-76`).
- **No environment variable is set, cleared or passed on by Theseus**:
  - `Abstractions/ProcessLauncher.cs:93-98` never touches `ProcessStartInfo.Environment`;
  - a repo-wide search for `Environment[`, `EnvironmentVariables` and `SetEnvironmentVariable` finds nothing.
  - `claude` therefore inherits the launcher's environment, which comes from Windows Terminal.
- `LaunchSpec` (`Launching/LaunchSpec.cs`) carries session id, mode, executable, working directory, name, prompt,
  add-dirs, MCP config and per-session settings.
  - **No field carries an environment variable or a role.**
  - `claude` is given `--resume|--session-id`, `-n`, the prompt, `--add-dir`, `--settings`, `--allowedTools` and
    `--mcp-config` (`Terminal/ClaudeArgumentBuilder.cs:51-89`).

**A card.**
- A plan file, `<PlanDirectory>\<key>.json`, is written by sessions and holds title, colour, cwd, addDirs, prompt and
  jiraKeys (`Domain/SessionPlan.cs`).
- A sidecar entry in `board.json` holds what sessions must not lose when they rewrite their plan (`Domain/WorkstreamSidecar.cs:128-165`).
  - `EveryStartPrompt` lives there for exactly that reason (:162-164).
  - A role would belong there too.

**Proposals and approval.**
- `propose_workstream` creates a pending proposal, with title, brief, done criteria and report-back (`Mcp/McpHandler.cs:655-700`).
- Approving it copies the parent's cwd and writes a plan whose prompt says to read the brief.
- It then adds a `HandsOver` link from the parent and, when report-back was asked, a `ReportsBack` link with the
  report path (`ChainsViewModel.cs:266-276`).

**Reports.**
- `report_back` records a document, a summary (2,000 characters at most) and an optional verdict on the links this
  card writes (`Links/LinkBook.cs:314-406`).
  - Verdicts: done, partial, go, needs-clarification, kill.
- The owner confirms on the board, via the Needs-you row or the drawer → `ChainsViewModel.Confirm` → `LinkBook.Confirm`.
  That sets the link satisfied and changes nothing else.
- The other card sees the report through the `related` tool, with a note to treat summaries as data.

**Stopping.**
- Every stop begins with a press (Stop, Stop selected, Pause). **There is no automatic stop anywhere.**
- Confirming a report has no hook of its own: `LinkBook.Changed` refreshes the board, and that is all.

**The holdout guard** (`~/.claude/bin/holdout-guard.js`: the owner's own file, **not in this repo**).
- A user-level PreToolUse hook on every tool. It denies a tool call whose path, command, URL or pattern names the
  holdout repo, unless the session's environment has `THESEUS_HOLDOUT_AUTHOR=1`.
- It relies on hooks inheriting the environment of the `claude` process, which a session's own shell cannot change.
- It is all or nothing: an author may read **and** write. It is a tripwire for accidents, not a wall (its own comment).

## What the objective needs that does not exist

| Need | Gap |
|---|---|
| A session Theseus starts with holdout access | Theseus sets no environment variables; the guard's only key is one |
| Read but not change the holdouts, and not change code | The guard has no read-only mode, and nothing restricts writing elsewhere |
| A card that knows it is a verifier | No role on cards, specs or MCP sessions |
| Start it from the card being verified, in one action | Follow-on cards exist (approval copies cwd and links), but only from a proposal or Spawn follow-on, with no role or access |
| The verdict back to the implementing card, nothing more | `report_back` and `related` already do this; the summary must be kept to name, result and reason |
| End the verifier once its report is confirmed (Nigel's idea, 2026-11-19) | No automatic stop exists, and no hook on confirm |

## Failure modes measured or found (P15)

- **Whether a PreToolUse hook sees an environment variable set on the `claude` process** is the guard's assumption
  and the whole mechanism. **Not yet measured** on this machine. The guard's comment asserts it, and it was set up on
  2026-11-18, but no session with the variable has been started since. The spec should require it to be measured
  first, with one launched session, before anything is built on it.
- **A resume that fails within 5 s is restarted under a new session id**, but its MCP config still names the old id
  (`ClaudeProcessRunner.cs:38-54`; `BoardLaunchEventSink.cs:70-71` only remembers the session). That session's
  `report_back` would come from an id Theseus does not know. This is an existing defect, found here; it matters for a
  verifier because its report is the whole point. Recorded, out of scope unless Nigel decides otherwise.
- **The guard matches its own repo's path.** A generic version for this public repo must take the holdout location from
  settings, not hold the owner's repo name (P19).

## Options for the decisions the spec needs

**D1. How a verifier gets holdout access**

| Option | Trade-off |
|---|---|
| **A. A role on the card, turned into an environment variable by the launcher** (recommended) | `LaunchSpec` gains an environment map, filled from a `Role` on the card's sidecar entry (verifier → `THESEUS_ROLE=verifier`). It is the guard's own mechanism (the session cannot change its hooks' environment) and is testable in Core. The first step measures that the hook sees it |
| B. A per-session `--settings` hook that allows the holdout | Settings are passed per session already, but a user-level deny still applies alongside, so it cannot grant access past it |
| C. A separate Windows account for verifiers | A real wall, but heavy, and far beyond a tripwire |

**D2. Where the guard lives, and its read-only mode**

| Option | Trade-off |
|---|---|
| **A. A generic guard in this repo, installed like the lock guard** (recommended) | `tools/holdout/holdout-guard.js`. It reads the holdout location from Theseus's settings (P19: no owner path in the repo). Roles: none (no access), verifier (Read, Grep and Glob on the holdout; no Write, Edit or Bash naming it), author (all). Tested like the lock guard. The owner's personal guard is retired once it is installed |
| B. Add the read-only mode to the owner's personal guard | Quicker, but untested and outside the method's evidence, and nobody else could use it |

**D3. Where the verifier writes**

| Option | Trade-off |
|---|---|
| **A. Its full result in the private holdout repo; the verdict through `report_back`** (recommended) | Steps and observations stay private. The implementing card sees only the summary, which the brief limits to the scenario name, pass or fail and one line. The owner commits the results, as he does every holdout change |
| B. Everything in the report document, in the notes folder | Simpler, but the document may describe the scenario, and the implementing session can read the notes folder |

**D4. Starting one, and the brief**

A board action on a card, "Verify with holdouts…". It creates a follow-on card with:
- role verifier, working directory the holdout clone (from settings), and the implementing card's repos as `--add-dir`
  so it can build and run them;
- a `HandsOver` link from the implementing card and a `ReportsBack` link to it;
- a fixed brief. Read the implementing card's spec for the named scenarios. Run each. Ask the owner for steps only a
  person can do. Report each verdict in the summary only as name, result and reason. Write nothing in the code repos.

Nothing starts until the owner presses Start, as for every card.

**D5. Ending itself (Nigel's idea)**

When the owner confirms a verifier card's report, Theseus stops that card, the same way a Stop press does, so close-step
closes it about 20 s later. **This would be the first stop that is not a press.** It follows the owner's own confirm,
on a card whose only job is done. Only verifier cards; no other card stops on its own.

## Out of scope

- The project rows and stage columns (objective 1.1), and reading which stage a card is at. The owner decides when to
  verify.
- Automating scenarios on CI (objective 1.4).
- The remint defect above, unless Nigel adds it.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| research | logged at the stage start in the workstream log | this commit's time | A read-only explorer mapped launch, links, reports and MCP (its report quoted above, with file references); the guard read directly |
