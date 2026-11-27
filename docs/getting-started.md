# Getting started with the agentic factory

The **agentic factory** is a method for running software work with Claude Code sessions. Work is planned from
objectives with acceptance criteria. Each piece of work goes through fixed stages, writes its records to disk, and
passes gates: the owner accepts each spec, an independent verifier checks the result, and only a person merges into
main. The terms used here (workstream, pipeline, gate, holdout, park) are the ones the [software factory guide](factory-guide.md) uses.

The method ships as a **Claude Code plugin**, `agentic-factory`, which works in plain Claude Code on Windows, macOS
and Linux. It gives every session:

- **the method's standing rules**, and its "decide, record, or park" ladder for choices a session meets on its own;
- **a skill per stage**: `research`, `spec`, `plan`, `implement`, `verify` and `review`;
- **commands**: `new-project`, `new-workstream`, `handoff`, `report`, `verifier` and `check`;
- **two guards** on every tool call. The **trunk guard** refuses pushes to `main`, `master` or `trunk`, and merging,
  approving or abandoning a pull request, and asks before a branch is deleted or force-pushed. The **holdout guard**
  keeps a builder out of the holdout scenarios that judge its work.

On Windows, **Theseus** adds a board on top: cards, the project view, locks, verifier cards started for you, and
gates you approve with a click. It is optional. Everything below works without it.

## 1. Prerequisites

| Tool | Version | Check with |
|---|---|---|
| Claude Code | 2.1.295 or later | `claude --version`; `claude update` to upgrade |
| Node.js | 18 or later, on the PATH | `node --version` |
| git | any current release; on Windows, Git for Windows, which also gives Claude Code its Bash | `git --version` |

The guards are Node scripts. **If Node is missing, every tool call is refused**, on purpose, rather than let a session
run without its guards. Claude Code older than 2.1.295 cannot do that and lets the call through, which is why that
version is the minimum.

## 2. Install the plugin

In a terminal:

```bash
claude plugin marketplace add nigelbasel/Theseus
claude plugin install agentic-factory@theseus
```

Or, inside a Claude Code session, `/plugin marketplace add nigelbasel/Theseus`, then
`/plugin install agentic-factory@theseus`.

To install from a branch other than the default one, add it after a `#`: `nigelbasel/Theseus#<branch>`.

The plugin is installed for you (user scope), so it is in every session you start. Hooks load when a session starts:
start a new session after installing. `claude plugin update agentic-factory` brings it up to date, and
`claude plugin disable agentic-factory` turns it off.

## 3. Check it is in place

In a new session:

```
/agentic-factory:check
```

It reports, one line each, the versions of Claude Code, Node and git, whether the plugin is installed and enabled,
whether its method files are present, and whether the guards decide sample calls as they should. It ends with
"Everything the method needs is in place." or the number missing. Give it a project's home folder to check that
project's file as well: `/agentic-factory:check <home folder>`.

## 4. Your first project and workstream

1. **Start a project.** In a session, `/agentic-factory:new-project "<title>"`. It asks for the objective, a home
   folder for the project's records, the repos it changes, the primary branch and the acceptance criteria. Then it
   writes `project.md` from the method's starter, and offers to create the primary branch in each repo.
2. **Start a workstream.** `/agentic-factory:new-workstream <slug> <home folder>`. It asks which objective the work
   serves and which pipeline it runs (`feature`, `bug` or `freeform`). Then it adds the workstream to `project.md`,
   branches `<user>/<slug>` off the primary branch with a worktree per repo, writes `work/<slug>/brief.md`, and gives
   you the command that starts the workstream's session.
3. **Work the stages.** In that session, each stage's skill says what to produce and what has to be true before
   moving on. For a feature: research, then spec, where the session **stops** for you to accept it, then plan and
   implement.
4. **Verify independently.** `/agentic-factory:verifier <slug>` gives you the command to start a verifier in a fresh
   terminal. It checks the branch against the spec and your holdout scenarios, and reports a verdict.
5. **Report and hand over.** `/agentic-factory:report` writes `work/<slug>/REPORT.md`, including everything the
   session decided without you. `/agentic-factory:handoff` writes what the next session needs to carry on.
6. **Merge.** The workstream merges into the project's primary branch once its suite passes. One pull request takes
   the primary branch into main, and you complete it.

## 5. Two paths

### Windows: Theseus and the plugin

1. Install Theseus with `Theseus.msi` from the newest release at https://github.com/nigelbasel/Theseus/releases.
2. **Let the setup run.** The first time Theseus opens with no projects folder, **Set up Theseus** opens by itself and
   shows only what is still missing, one step at a time, with **Back**, **Next**, **Skip setup** and **Finish**:
   - **Folders.** A **projects folder** (needed for projects; outside any code repo: each project's `project.md` and
     records go there), and optionally a **worktrees folder** and a **clones folder**. With the last two set, Theseus
     makes each workstream's branch and worktree for you. Section 6 suggests a layout, and
     [How your work is laid out](project-layout.md) explains each folder.
   - **The stop hook**, which lets Stop ask a session for its handover. The step shows the exact entry; **Add the hook
     entry…** shows the change to `%USERPROFILE%\.claude\settings.json` again and makes it only if you agree, keeping the
     old file beside it as `settings.json.previous`.
   - **The guards**: the holdout, trunk and tests-lock guards, each with the same **Install** and hook entry as
     **Tools > Options > Guards**. Until every guard is set up, the board names the ones missing or out of date.
   - **The tools**: Claude Code 2.1.295 or later, Node, git and the `agentic-factory` plugin. It reports what it finds
     and installs nothing.

   Once you finish or skip it, it does not open by itself again; **Tools > Set up Theseus…** opens it whenever you like.
   **File > New project…** with no projects folder chosen opens its folder step.

   **By hand, if you prefer.** Choose the folders in **Tools > Options > Folders**. Register the hook by merging this
   into `%USERPROFILE%\.claude\settings.json` (into the `hooks` you already have, if any), then start new sessions:

   ```json
   {
     "hooks": {
       "Stop": [
         { "hooks": [ { "type": "command", "command": "\"C:\\Program Files\\Theseus\\Theseus.Hook.exe\"" } ] }
       ],
       "UserPromptSubmit": [
         { "hooks": [ { "type": "command", "command": "\"C:\\Program Files\\Theseus\\Theseus.Hook.exe\"" } ] }
       ]
     }
   }
   ```

   **Help > About** says whether the hook is registered ([Starting and stopping](starting-and-stopping.md)).

   **Auto mode.** In **Tools > Options > Guards**, enter your source-control hosts, internal domains and production
   words once, then **Write…** auto mode's environment, so auto mode treats pushing your own branches as routine. The
   change to Claude Code's settings is shown first and made only if you agree.
3. Install the plugin as in section 2. Sessions Theseus starts read the same Claude Code configuration, so they get
   the same plugin: the same rules, skills and guards, from the same files.
4. Make projects with **File > New project…**. The board then does what the commands above do by hand: cards for
   workstreams, the spec gate on the project view, and a verifier card when a holdout scenario names a build.

### macOS: the plugin only

Theseus is a Windows app, so on a Mac there is no board. Install the plugin as in section 2, and run the method
by discipline:

| With the board | On a Mac |
|---|---|
| The spec gate is a click on the board | The session stops at its spec. You accept it explicitly ("accept spec.md"), and you or the session record your words as a dated row in the project file's Decisions table before it plans. Nothing stops a session writing that row itself, so this gate rests on discipline |
| A verifier card starts when a scenario names a build | You start the verifier yourself, in a new terminal, with the command `/agentic-factory:verifier` gives |
| `ask_owner` shows a question in Waits for you | A parked question is written to `work/<slug>/questions.md`, with a `Waiting: owner:` line under its criterion |
| `report_back` files a report for you to confirm | The report is `work/<slug>/REPORT.md`; you read it |
| Locks, duties and the overnight queue | Not available: run one container-backed suite at a time yourself |

The guards work the same on macOS. Keep each project's holdout scenarios in a private repository cloned beside the
code as `<repo>-holdout`: the holdout guard protects any folder named that way, wherever it is.

## 6. Suggested directory conventions

These are suggestions. Every developer organises their own machine, and neither the plugin nor any command assumes a
layout: each asks where things go.

```
<code>/<host>/<owner>/<repo>/        clones, on their default branch, pull-only
<code>/<host>/<owner>/<repo>-holdout/  a project's private holdout scenarios, beside its repo
<work>/<workstream>/<repo>/          a worktree per repo, on <user>/<workstream>
<projects>/                          a local git repository with no remote
<projects>/<project>/project.md      a project's home folder
<projects>/<project>/work/<slug>/    each workstream's records
```

Branches: one **primary branch** per project, `<user>/<project>`, off main; one branch per workstream,
`<user>/<workstream>`, off the primary. Workstreams push and merge into the primary freely; only the pull request into
main waits for a person. A workstream branch cannot be `<user>/<project>/<workstream>`, because git cannot hold a
branch and a folder of branches under one name. These are the method's own rules, not only suggestions: "Branches,
worktrees and merging" in `method/README.md` gives them in full, and the plugin puts them into every session. In
Theseus, **New project** suggests the primary branch as `<user>/<project>` from your user name, and its box
**Workstreams may branch off the primary branch, push, and merge back into it without asking** starts ticked; untick it
to have each workstream ask first.

Records stay out of code repos, so specs and evidence never need a branch or a pull request. Keeping the projects
folder in its own local git repository keeps every version of every spec.

## 7. When something is wrong

- **New project asks for a folder in Theseus:** no projects folder is chosen yet. It opens the setup's folder step;
  **Tools > Options > Folders** also chooses one (section 5).
- **Every tool call is refused, mentioning node:** Node is missing or not on the PATH. Install it and start a new
  session.
- **`/agentic-factory:...` is not offered:** the plugin is not installed, or not enabled for this configuration.
  `claude plugin list` shows it. Start a new session after installing.
- **A push or a merge is refused:** that is the trunk guard. Push the workstream or primary branch instead, and raise
  a pull request for a person to complete.
