# Verifying with holdouts

Holdout scenarios are checks your changes are judged by that the sessions doing the work never see. They live in a
private repository of your own. A **verifier card** runs them for you and reports back only a verdict, so the session
that made the change learns whether it passed, not what the check was.

## Setting it up

Nothing needs setting: with no folder chosen, each project's scenarios go in a folder of its own under Theseus's data
folder, `%LOCALAPPDATA%\Theseus\holdouts\<project folder name>\`, made the first time it is used. It is outside every
repository and the projects folder. **Tools > Options** shows the folder in use, marked **(default)**.

To keep the scenarios somewhere else, such as a private repository of your own:

1. Clone it somewhere on this machine.
2. In **Tools > Options**, choose **Change…** beside **Holdout scenarios folder** and pick the clone, and set **Holdout
   remote address** to where it lives, such as `host/owner/repo`. A folder you choose is kept and used for every project.
3. Install the holdout guard (`tools/holdout/holdout-guard.js` in the Theseus repository) as a PreToolUse hook for every
   tool in your Claude Code settings, the same way as the lock guard. It reads the folder and address from Theseus's
   settings, so it holds no path of its own.

With the guard installed, a session with no role is refused reading, searching or fetching in the holdout. A verifier
can read the scenarios and write only its report and its handoff, in the holdout's `results` folder. No session runs a
shell command inside the holdout folder, and none writes Theseus's own settings or board with the file tools. A session
started with `THESEUS_HOLDOUT_AUTHOR=1`, as a scenario-writing card's are, can change the scenarios. With no folder
chosen, the guard covers the whole default holdouts folder in the same way.

The guard is a tripwire against accidents, not a wall. It reads the paths, commands and addresses a tool is given, and
the session's working directory. A search rooted in a folder above the holdout is not refused, so it could return
scenario lines; and a shell command can still do what the file tools are refused.

## Writing the scenarios

While a project has no scenarios yet, **Verify with holdouts…** is shown disabled, saying "No holdout scenarios for this
project yet", and **Write scenarios…** beside it creates a card, **Write holdout scenarios for <project>**, that works in
the project's holdout folder and is the one kind of session allowed to write there. It writes scenarios, never code, and
starts when you start it. If the folder you chose is missing, **Set the holdout folder…** opens Options at that setting.

## Verifying a change

1. When a card's change is ready to verify, choose **Verify with holdouts…** from the card's menu, or tick the card
   and use **Workstream ▸ Verify with holdouts…**.
2. A new card, **Verify: <card title>**, appears beside it, linked both ways. It runs in the checked card's folder, so
   it can build and run the change, with the holdout folder in reach for reading the scenarios. Nothing starts until
   you press Start. The action is unavailable until the holdout folder is set.
3. Its session reads which scenarios the change's spec names, runs each, and asks you in its own tab for any step only
   a person can do. It writes the details to a dated file in the holdout's `results` folder, and reports back to the
   checked card with only each scenario's name, pass or fail, and one line.
4. Read the report on the board and confirm it. Confirming a verifier's report also stops the verifier card, the same
   way Stop does; it closes once it has handed over and gone quiet.

Commit and push the `results` files yourself, as you do any change to the holdout repository.
