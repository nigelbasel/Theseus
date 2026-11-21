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

A card can belong to a project. Each project is a folder inside the **Projects folder** you set in **Tools >
Options**, holding a `project.md` in the method's format and a `work` folder of its workstreams' records, all plain
files kept outside your code repositories. Theseus watches the folder and reads a project again as soon as a file in it
changes. With no projects folder set, nothing here appears.

The project file says which cards are in the project: each row of its workstreams table names a card by its name on the
board, with the name of the piece of work it does, which is also the name of its records folder. A card named in
several rows has worked on several pieces of work, and its last row is its current one. A verifier card goes with the card it checks. To change it, use a card's
**Add to project…**, **Remove from project** or **Convert to project…**, or edit `project.md`: both change the file,
and the board follows within a couple of seconds. In the project view, select a piece of work to tick its objective's
criteria and **Edit objective…** in the Properties pane; use the project card's menu for **Add objective…**.

Every project in the folder shows on the board as one **project card**. It is as wide as a card, with a stripe and a
stacked-card mark in the project's own colour and the word **PROJECT** beside its title, so it is never mistaken for a
workstream. Under the title are the project's current phase, how many objectives are done, and the project view's own
sentence saying where it is, trimmed to one line (hover over it to read it all), then its counts: done, busy, waiting on
you, waiting for their holdout check, stuck and still to do, leaving out any that are zero. The badge counts what is
waiting on you; hover over it to list it. The project's own cards leave the board while the card stands for them, and
come back if the project can no longer be read.

A project card is selected, dragged and acted on as a card is: click, **Ctrl**+click, **Shift**+click, a rubber band,
**Ctrl+A**, **Ctrl+I**, **Select live** (which picks a project when any of its cards has a session) and **Esc** all
include it, and a selected one has the card's accent border. Drag it to any column and row, where it sits above that
cell's cards and stays across restarts. Cards and project cards are moved only while the columns show stages; dragged
while they show session state, nothing moves and the status bar says to choose **View > Columns by > Stage**. Commands for one workstream,
such as **Properties** and **Verify with holdouts…**, are off while only projects are selected and say why; any command
on a selection that holds a project and cards acts on the cards, and the status bar says the project was skipped.

▷ and □ on a project card, **Start…** and **Stop** on its menu, and **Start selected** and **Stop selected** with the
project selected, start and stop the whole project. Start asks first, listing each of the project's cards with a tick
box: a card that can start is ticked; a paused card is listed unticked, and starts only if you tick it, which also takes
its paused mark off; a card that is running, blocked, done or waiting on you is greyed with the reason and is never
started. The ticked cards start in the project's terminal window, as their own ▷ would start them. Stop asks each of the
project's sessions to write its handover and stop, as each card's □ does. With nothing to start or stop, each is off and
its tooltip says why, such as "No card of Theseus bootstrap can start: git-22 is running".

A project is given its own colour when it first shows, from a set of eight project colours that are never used for
workstreams, so a project card never shares a colour with a card; two projects get different colours while there are
colours left. **Project properties…** on its menu shows its title and the path of its `project.md`, and is where the
colour is changed, from the same eight; a dot marks a colour another project has, and hovering over it names the
project. The card, the project's tab and its summary in the project view all take the colour.

One whose `project.md` does not read is dimmed and says why, keeping its mark, colour and the word PROJECT but with no
▷ or □; double-click it to open the file. Double-click a project card that reads, press **Enter** on it, choose
**Open** on its menu, or **View > Open project**, to fill the main area with the project: see
[The project view](project-view.md). A spec waiting for you shows **Waiting on you**, with
**Open spec** and **Accept spec…**, which asks first and names the version it accepts; a spec changed after you accept
it waits for you again. **← Board** at the top, **Alt+Left**, or **View > Back to the board**, goes back.

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
