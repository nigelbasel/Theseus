# Verifying with holdouts

Holdout scenarios are checks your changes are judged by that the sessions doing the work never see. They live in a
private repository of your own. A **verifier card** runs them for you and reports back only a verdict, so the session
that made the change learns whether it passed, not what the check was.

## Setting it up

Nothing needs setting. Each project's holdout folder is found by convention, in this order:

1. a folder you chose for the project in **Tools > Options** (or the folder for every project, if you set that);
2. a folder named after the project's home code repo with `-holdout`, beside its clone: the clone of `Theseus` at
   `...\Theseus` gives `...\Theseus-holdout`. The home repo is the first code repo in the project's Repos table (not a
   `local/` notes folder or a wildcard), else the repository its cards work in; a worktree leads to its primary clone;
3. otherwise Theseus's own, `%LOCALAPPDATA%\Theseus\holdouts\<project folder name>\`, made the first time it is used as a
   local git repository, so the scenarios keep a history with no remote. Cards in no project use `holdouts\ad-hoc`.

**Tools > Options** lists each project's folder and how it was found ("chosen by you", "found beside the repo",
"Theseus's own"), with **Change…** and **Reset**, and **Keep these in a private repository…** on Theseus's own, to add a
remote of yours. Change one only if you understand what the guard then protects; a whole drive or a relative path is
refused. Theseus writes the folders to `%LOCALAPPDATA%\Theseus\holdout-folders.json`, which the guard reads; a folder
once listed stays listed, so a change of convention never leaves its scenarios unguarded. The guard also treats any
folder named `<something>-holdout` as a holdout, however a session names it.

To keep a project's scenarios in a private repository of your own, clone it beside the project's home repo as
`<repo>-holdout`, or choose it with **Change…**, and set **Holdout remote address** to where it lives, such as
`host/owner/repo` or the URL you clone it by (one address for all projects); the guard refuses its https, SSH and
`owner/repo` forms alike.

## Installing the guard

**Tools > Options, Holdouts** shows the installed guard: *Not installed*, *Up to date*, or when an older one was
installed. **Install the holdout guard** copies this Theseus's guard to where your Claude Code hook runs it (by default
`bin\holdout-guard.js` in your Claude Code folder), keeps the copy it replaces as `holdout-guard.js.previous`, then
checks it on a folder made for the check: a session with no role must be refused a read of the holdout, and a verifier
allowed it. **Undo** puts the previous copy back. If Claude Code's settings do not run the guard at all, Options says so;
**Add the hook entry…** shows the exact change to `settings.json` and makes it only if you agree, keeping the old file
as `settings.json.previous`. Hooks are read as a session starts, so a newly registered hook guards sessions started
after it. The guard reads the folders and the address from Theseus's files, so it holds no path of its own.

After an update that brings a newer guard, the board says once that the holdout guard is out of date.

With the guard installed, a session with no role is refused reading, searching or fetching in the holdout. A verifier
can read the scenarios and write only its report and its handoff, in the holdout's `results` folder. No session runs a
shell command inside a holdout folder, and none writes Theseus's own settings, board or `holdout-folders.json`, the
installed guard or its previous copy, or Claude Code's `settings.json`, with the file tools; the rest of the Claude Code
folder stays writable. A session started with `THESEUS_HOLDOUT_AUTHOR=1`, as a scenario-writing card's are, can change
the scenarios. With no folder chosen, the guard covers the whole default holdouts folder in the same way.

The guard is a tripwire against accidents, not a wall. It reads the paths, commands and addresses a tool is given, and
the session's working directory, and reads each word of a shell command as a path from where it runs, so `../holdout`
counts as naming the holdout. A search rooted in a folder above the holdout is not refused, so it could return
scenario lines; and a shell command that builds a path the guard cannot see (a variable of its own, a short 8.3 name,
a quote inside a name) can still do what the file tools are refused.

## Writing the scenarios

While a project has no scenarios yet, **Independent check…** is shown disabled, saying "No holdout scenarios for this
project yet", and **Write scenarios…** beside it creates a card, **Write holdout scenarios for <project>**, that works in
the project's holdout folder and is the one kind of session allowed to write there. It writes scenarios, never code.
Theseus starts it at once, and each time it starts it is aimed at the project's holdout folder of that day, so a folder
changed since it was made is followed. If the folder you chose is missing, **Set the holdout folder…** opens Options at
that setting.

Each scenario names the work it checks in a line of its own, such as `Checks: verifier-runs, close-step`, giving each
piece of work by its slug (or a card in no project by its key). Theseus reads those lines, and makes a verifier only for
a build some scenario names: otherwise **Independent check…** says "No holdout scenario names <slug>", and offers
**Write scenarios…**, whose writer is given the project's slugs and adds the line to scenarios already there. So one
folder's scenarios never judge work they were not written for.

## Verifying a change

In a project, Theseus runs the checks itself. When a build waits for its independent check and a scenario names it, and
its own session has stopped, Theseus makes its verifier and starts it. When some waiting build is named by no scenario
and the project has no scenario writer, Theseus makes one and starts it. Meanwhile a line under **Waits for you** says
what Theseus is doing itself, such as "Writing scenarios for 7 builds…" or "Checking 3 builds…", with nothing to press;
it is not counted as waiting for you. A project you stopped starts
nothing until you start it. You need not start a check: if Theseus could not start one, its line in **Waits for you**
says why, with **Start again**. What waits for you is the report each check files, to confirm or reject.

1. To check one card by hand all the same, choose **Independent check…** from the card's menu, or tick the card and use
   **Workstream ▸ Independent check…**.
2. A new card, **Verify: <card title>**, appears beside it, linked both ways. It runs in the checked card's folder, so
   it can build and run the change, with the holdout folder in reach for reading the scenarios. Theseus starts it at
   once. A verifier that is neither running nor starting and has not reported, after a failed start or a restart of
   Theseus, shows in the build's Waits line with **Start again**. A build stays checked from the moment its verifier is
   made until it reports or is archived, so it gets no second verifier, and a card already starting (its tab perhaps
   asking whether to trust its folder) is not started again while Theseus runs.
3. Its session runs the scenarios whose Checks line names the change, and asks you in its own tab for any step only
   a person can do. It writes the details to a dated file in the holdout's `results` folder, and reports back to the
   checked card with only each scenario's name, pass or fail, and one line.
4. Read the report on the board and confirm it. Confirming a verifier's report also stops the verifier card, the same
   way Stop does; it closes once it has handed over and gone quiet.

Commit and push the `results` files yourself, as you do any change to the holdout repository.
