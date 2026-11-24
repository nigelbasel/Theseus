# Starting and stopping

[Help home](index.md)

## Starting a session

**Start** opens a Windows Terminal tab, runs Theseus's launcher in it, and hands the launcher a ticket.
The launcher collects the session's instructions over a named pipe and starts Claude Code in the
workstream's folder. The session's identifier is chosen before the tab exists, which is what makes the
session resumable tomorrow.

A workstream that has run before is not given its original prompt again, because that prompt
describes starting the work. It is told to carry on from its handover notes instead. Every session is
told which workstream it is and where its handover notes go, which is why typing "stop and hand over"
into its tab always works.

One press of **Start** opens the tab. If a tab of the same name is already open with nothing running in it, the new
tab opens anyway and the status bar says the older one is there, so you can close it.

### Folder trust

Claude Code asks you to trust each folder a session starts in, or adds, the first time. For an added folder its answer
lasts only that session, so a card that adds folders asks every time it starts. **Tools > Options > Folder trust** lists
the folders your cards use that Claude Code will ask about. **Trust these folders…** shows the exact change to Claude
Code's configuration file and makes it only if you agree, as if you had answered Yes once in each. Trusting a repository
covers every worktree of it. **Undo** takes back only what Theseus set.

### When starting goes wrong

| What happens | Why, and what to do |
|---|---|
| "That launch ticket is unknown or has already been used" | The tab reached a different copy of Theseus, or the ticket expired (it lasts two minutes). Usually two copies of Theseus were running. Close both and start again. |
| The tab opens in the wrong terminal | Theseus prefers a Windows Terminal that is already running, and the stable build over Preview. Set the path in **Tools > Options**. |
| Start does nothing | Claude Code or Windows Terminal could not be found (the status bar says which), or the card is already following a live session. Set the paths in **Tools > Options**. |
| Claude Code asks you to trust a folder in the tab | The folder has no trust record yet. The card says it needs you to trust the folder, and **Focus** brings the tab forward. Answer it there, or trust it once for every start in **Tools > Options > Folder trust**. |
| A tab says Claude Code exited with a code | The session failed, and the tab is held open for 15 minutes so you can read why. Press Enter to close it sooner. A held tab that is never closed is cleared up the next time Theseus starts. |
| A question about paused or blocked work | The card is marked. See [the board](board.md#paused-and-blocked). |

## Stopping a session

**Stop** writes a request naming the session and the file it should write. A hook in your Claude Code
settings runs at the end of every turn. When it finds a request for its session, it hands the request
over instead of letting the turn end. The session writes its handover notes and stops. A request is
delivered exactly once.

An idle session has no turn coming, so Theseus starts one by typing a single word into that session's
own console. Nothing comes to the front and the window you are working in keeps the keyboard. Once the
handover is written and the session has gone quiet, Theseus types `/exit` the same way and the tab
closes. A session whose handover was already up to date, written earlier in the same run, closes once it
has answered the stop. Until the tab has closed, its card says it was asked to stop and is waiting.
A session that answered a stop more than ten minutes ago and is still open is asked afresh the next time it
is stopped, and a stop asked for after Theseus restarts is never held back with the ones it found waiting.

### Sessions that end by themselves

A session whose work is finished is asked to hand over and exit without anyone pressing **Stop**, once
it has been idle for half a minute with no question of its own unanswered. Its card's history says why.

| Card | Ends once |
|---|---|
| A verifier | It has filed its report. |
| A card that reports back | You have accepted its report, and no work has been handed to it since. |
| A scenario writer | It has written its scenarios. |

A project's lead never ends by itself. A project's sessions can also stop its verifiers and scenario
writers themselves, as they can stop any other card of the project.

### Work given to an open session

When a session starts a card whose session is already open and idle, the instruction it gives is
handed in as that session's next turn, the same way, and kept in the card's history as a hand-in. Only
a session that is in the middle of a turn, or waiting for you to answer it, turns the start away.

You can always stop a session yourself: type "stop and hand over" into its tab. The button only saves
the typing.

### When stopping goes wrong

| What happens | Why, and what to do |
|---|---|
| Nothing, and the hook is not registered | Nothing will be delivered. Theseus reads your Claude Code settings but never writes them. Run `install.ps1`, which prints the configuration to add. **Help > About** says whether the hook is registered. |
| Nothing, and the hook is registered | Hooks load when a session starts, so a session older than the hook does not have it. Restart that session. This is the usual answer. |
| The session is sitting idle | Theseus types into its console to start the turn that carries the request. If it cannot reach that console, it brings the tab to the front so you can press Enter. |
| The terminal runs as administrator | Windows will not let Theseus type into a more privileged window. Stop that session by hand, or run its terminal normally. |
| A request from yesterday | Requests expire after four hours, so one made in the evening cannot interrupt the next morning. |
