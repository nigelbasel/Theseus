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

## Projects

A card can belong to a project: a project file, in the method's format, in a repository you list under **Project
repositories** in **Tools > Options**, one folder per line. Theseus reads each project from its primary branch with
git, whichever branch the folder has checked out, and fetches nothing. While at least one project can be read, the
card's menu and the **Workstream** menu have **Project**: choose a project, then the workstream slug the card works
under there (the slugs its project file lists), or **No slug yet**, or **Not in a project**. A slug another card
already works under is refused, except for a verifier card, which shares the slug of the card it checks. With no
project repositories listed, nothing here appears.

A project that can be read shows on the board as one **project card**, which you drag to the column you want. It shows
the project's current phase, how many objectives are done, and a count of what is waiting on you; hover over the count
to list it. The project's own cards leave the board while the card stands for them, and come back if the project can no
longer be read. Double-click the project card, choose **Open** on its menu, or **View > Open project**, to fill the main
area with the project: its phases, objectives and workstreams on the left, and on the right each objective's
workstreams at the stage their files have reached. A spec waiting for you shows **Waiting on you**, with **Open spec**
and **Accept spec**, which asks first and names the commit it accepts. **Board** at the top, or **View > Back to the
board**, goes back.

## The buttons on a card

| Button | What it does |
|---|---|
| ▷ **Play** | Starts or restarts this workstream's session. |
| □ **Stop** | Asks the session to write its handover notes when its turn ends. When the notes appear and the session has been quiet for 20 seconds, Theseus types `/exit` into the tab so it closes. Nothing is ever killed: a killed session writes nothing. While the stop is under way the card's icon turns amber and does nothing when clicked; hover over it to see how far the stop has got. |
| **Close now** | Appears only when Theseus could not type `/exit` and nothing was typed, for example because the terminal was not in front. It tries once more, bringing the terminal to the front, and types only if the session is idle and quiet at that moment. If Windows refused the keystrokes, or the terminal runs as administrator, the amber icon says to close the session yourself instead. |
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
| **Jira 12 ▾** | The card's Jira issues. Click to list them with their titles and open one. Keys come from the opening prompt, the card itself and the session's plan file; titles come from the session, which reports each issue it works on. Taking a key off a card is remembered. |
| **PR 22174 ▾** | The card's pull requests, with titles where the session reported them. When the card's repositories have more than one remote, or none, a number cannot be tied to one of them, so it is not linked, and hovering it says why. |
| on nigel/ledger | The git branch checked out. If the card's extra directories include repositories (a clone, a worktree, or a feature folder holding several), it is their branch: "on nigel/ledger · 3 repos" when they agree, **branches differ** in amber when they do not, with each repository's branch in the tooltip. Otherwise it is the branch of the card's working directory. |
| "shares … with …" in amber | Two workstreams share a handover document or a Jira issue. Two sessions writing the same file overwrite each other without either noticing, so give one its own document in its details. |
| "Restarted outside Theseus" | A session is running in this workstream's folder that the board is not following. **Adopt** it. |
