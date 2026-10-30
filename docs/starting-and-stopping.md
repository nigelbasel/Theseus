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

### When starting goes wrong

| What happens | Why, and what to do |
|---|---|
| "That launch ticket is unknown or has already been used" | The tab reached a different copy of Theseus, or the ticket expired (it lasts two minutes). Usually two copies of Theseus were running. Close both and start again. |
| The tab opens in the wrong terminal | Theseus prefers a Windows Terminal that is already running, and the stable build over Preview. Set the path in **Tools > Options**. |
| Start does nothing | Claude Code or Windows Terminal could not be found (the status bar says which), or the card is already following a live session. Set the paths in **Tools > Options**. |
| A question about paused or blocked work | The card is marked. See [the board](board.md#paused-and-blocked). |

## Stopping a session

**Stop** writes a request naming the session and the file it should write. A hook in your Claude Code
settings runs at the end of every turn. When it finds a request for its session, it hands the request
over instead of letting the turn end. The session writes its handover notes and stops. A request is
delivered exactly once.

You can always stop a session yourself: type "stop and hand over" into its tab. The button only saves
the typing.

### When stopping goes wrong

| What happens | Why, and what to do |
|---|---|
| Nothing, and the hook is not registered | Nothing will be delivered. Theseus reads your Claude Code settings but never writes them. Run `install.ps1`, which prints the configuration to add. **Help > About** says whether the hook is registered. |
| Nothing, and the hook is registered | Hooks load when a session starts, so a session older than the hook does not have it. Restart that session. This is the usual answer. |
| The session is sitting idle | Requests are delivered at the end of a turn, and an idle session has no turn coming. Theseus brings its tab to the front so you can press Enter. |
| The terminal runs as administrator | Windows will not let Theseus type into a more privileged window. Stop that session by hand, or run its terminal normally. |
| A request from yesterday | Requests expire after four hours, so one made in the evening cannot interrupt the next morning. |
