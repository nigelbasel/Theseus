# Cards

[Help home](index.md)

A card is one workstream. Its colour and wording follow its session, not its plan file.

## The buttons on a card

| Button | What it does |
|---|---|
| **Start** / **Restart** | Opens a tab for this workstream. The **▾** beside it offers the other way to start: resume the last conversation, or start fresh from the handoff. |
| **Stop** | Asks the session to write its handover notes when its turn ends. When the notes appear and the session has been quiet for a moment, Theseus types `/exit` into the tab so it closes. Nothing is ever killed: a killed session writes nothing. |
| **Focus** | Brings this session's tab to the front. The taskbar icon's menu does this too. |
| **Archive** | Takes the workstream off the board without losing anything. In the archive view the same button says **Restore**. Refused while a session is running. |
| **⌃** | Folds the card to its title. |
| **The tick** | Chooses the card for **Workstream > Start selected**, **Stop selected** and **Archive selected**. Nothing else decides what those act on. |

The **▾** menu also has **Paused**, **Blocked**, **Reopen tomorrow** and **Spawn follow-on**. Click a card
to open its details, where you can rename it, edit its setup, add Jira keys, see its links, or delete it.

**Delete** is only in the details, and asks first. It destroys the plan file. The handover notes and
the work itself are not Theseus's files and are left alone. It is refused while a session is running.

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
