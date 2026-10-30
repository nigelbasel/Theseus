# Cards

[Help home](index.md)

A card is one workstream. Its colour and wording follow its session, not its plan file.

## Selecting cards

Click a card to select it. **Ctrl+click** adds a card to the selection or takes it out, and
**Shift+click** selects every card from the last one you clicked to this one. Drag on an empty part of
the board to draw a box round several cards; hold **Ctrl** to add them to what is already selected.
Click an empty part of the board, or press **Esc**, to clear the selection.

The **Workstream** menu and the toolbar's start, stop and archive buttons act on the selected cards.

## Properties

Right-click a card and choose **Properties**, double-click it, or select it and press **Alt+Enter**.
Properties is where you rename a card, edit its setup, add Jira keys, see its links, or delete it.
While Properties is open it follows the card you click.

**Delete** is only in Properties, and asks first. It destroys the plan file. The handover notes and
the work itself are not Theseus's files and are left alone. It is refused while a session is running.

## The buttons on a card

| Button | What it does |
|---|---|
| ▷ **Play** | Starts or restarts this workstream's session. |
| □ **Stop** | Asks the session to write its handover notes when its turn ends. When the notes appear and the session has been quiet for a moment, Theseus types `/exit` into the tab so it closes. Nothing is ever killed: a killed session writes nothing. |
| ‖ **Pause** | On a running card: stops the session the same way and marks the card paused. On a stopped card: turns the paused mark on or off. Amber while the card is paused. |
| ↗ **Focus** | Brings this session's tab to the front. The taskbar icon's menu does this too. |
| **Archive** | Takes the workstream off the board without losing anything. In the archive view it restores it instead. Refused while a session is running. |
| ⌃ | Folds the card to its title. |
| ⋯ **More** | The card's menu, the same as right-clicking it: Properties, the two ways to start, Paused, Blocked, Reopen tomorrow and Spawn follow-on. |

Hover over any button to see what it does.

## What a card is telling you

| You see | It means |
|---|---|
| busy, idle, waiting for you | Read from the live session. The card is coloured and animated while its session is working. |
| ended | The session has gone, but it ran today. |
| not started today | No run of this workstream was under way today. The card says when the last run exited. |
| **Paused** or **Blocked** in amber | A mark you set. See [the board](board.md#paused-and-blocked). |
| Jira keys | Gathered from the opening prompt, the card itself and the session's own plan file. Taking a key off a card is remembered. |
| "shares … with …" in amber | Two workstreams share a handover document or a Jira issue. Two sessions writing the same file overwrite each other without either noticing, so give one its own document in its details. |
| "Restarted outside Theseus" | A session is running in this workstream's folder that the board is not following. **Adopt** it. |
