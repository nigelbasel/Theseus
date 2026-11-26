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
criteria and **Rename objective…** in the Properties pane; use the project card's menu for **Add objective…**.

Every project in the folder shows on the board as one **project card**. It is as wide as a card, with a stripe and a
stacked-card mark in the project's own colour and the word **PROJECT** beside its title, so it is never mistaken for a
workstream. Under the title are the project's current phase, how many objectives are done, and the project view's own
sentence saying where it is, trimmed to one line (hover over it to read it all), then its counts: done, busy, waiting on
you, waiting for their holdout check, stuck and still to do, leaving out any that are zero. The badge counts what is
waiting on you; hover over it to list it. The project's own cards leave the board while the card stands for them, and
come back if the project can no longer be read.

A project card is selected, dragged and acted on as a card is: click, **Ctrl**+click, **Shift**+click, a rubber band,
**Ctrl+A**, **Ctrl+I**, **Select live** (which picks a project when any of its cards has a session) and **Esc** all
include it, and a selected one has the card's accent border. It is dragged as a card is, only along what you place by
hand (between stages, between areas), and sits above its cell's cards across restarts. With columns by session state it
sits in the column of its most urgent running card (Needs you, then Working, then Idle), in Not running when none runs,
and in Done when all its work is done; with rows by folder or chain, in the row of its first card. With neither axis
placed by hand nothing can be dragged, and the status bar says to choose **View > Columns by > Stage** or **View > Rows
by > Area**. Commands for one workstream,
such as **Properties** and **Independent check…**, are off while only projects are selected and say why; any command
on a selection that holds a project and cards acts on the cards, and the status bar says the project was skipped.

▷ and □ on a project card, **Start** and **Stop** on its menu, and **Start selected** and **Stop selected** with the
project selected, start and stop the whole project: the project decides what runs. **Stop** remembers which of its cards
had a session at that moment, its run state, and asks each to write its handover and stop; the card then says
**⏸ Project stopped**, and the run state is kept across a restart of Theseus or of the machine. **Start** starts exactly
those cards again, with no list to tick. A project never stopped starts its first card: the card of the first piece of
work not done, else of the file's first row. A card that cannot start now (gone, archived, already running) is skipped and named in the status bar. A
card in both a selected project and the selection is started or stopped once, through its project. With nothing to
start or stop, each is off and its tooltip says why, such as
"No card of Theseus bootstrap can start: git-22 is already running".

Stopping one card of a running project by hand, or starting one, asks first: "<card> is part of <project>, which
decides what runs. Stop it anyway?" A card stopped that way says **Stopped by you**, and its project does not start it
again until you start it yourself. Cards of a stopped project, and cards in no project, are not asked about.

A card with no session says why it is stopped when there is more to say than "ended": **✓ Done** when its report back
was confirmed on the board ("Done: reported back 21 Nov") or it is in the last stage, with its face muted and its ▷
becoming **Restart for more work**, which asks what the more work is and starts it with that as its instruction (in a
project, where sessions restart done cards, it is a plain **Restart**, asking nothing);
**⏸ Project stopped: resumes with <project>**, where its ▷ starts the project; or **Stopped by you**. A done card stays
where you put it. Properties shows the same line.

A project is given its own colour when it first shows, from a set of eight project colours that are never used for
workstreams, so a project card never shares a colour with a card; two projects get different colours while there are
colours left. **Project properties…** on its menu is where its title and its colour are changed. OK writes a new title
to the first line of its `project.md`; if the file changed since the dialog opened, or the title is empty, it says so
there and writes nothing. Its colour, place and run state stay with the project's folder, so nothing else changes. The
path of its `project.md` is shown read only. Colours come from the same eight; a dot marks a colour another project has, and hovering over it names the
project. The card, the project's tab and its summary in the project view all take the colour.

The box **Workstreams may branch off <primary branch>, push, and merge back into it without asking** in Project
properties is yours to tick, and is kept by Theseus, never in `project.md`. It covers the primary branch the file names
when you tick it; if the file later names another, it gives nothing until you tick it again, and a trunk such as `main`
can never be granted. While it is ticked, each card of the project is told at its start that it may make its own branch
off the primary branch, named after its records folder, push it, and merge it back once its suite passes and its review
is done, and still to ask before any Jira item, pull request into the trunk, force push, deleted branch, or anything
outside the project's repositories. Such a card shows **project branch rules**, with the date you ticked it in its
tooltip. Untick it and its cards ask again from their next start; a session resumed after that is told the rules are
withdrawn.

One whose `project.md` does not read, or is in an older shape, is dimmed and says only **Needs updating**, keeping its
mark, colour and the word PROJECT but with no ▷ or □; the reason is in its tooltip and Project properties, for the
session that fixes it. Its **Update** button, a double-click or **Enter** starts a short session that brings the file into
the method's current shape, changing nothing else; the card says **Updating…** meanwhile. Once the file has changed, Theseus
asks that session to write its handover and exit once its turn has ended. If the file still does not read, the card says
**Still needs updating**, and Update starts that session again. Update waits while one of the project's own
cards is running. **Not a project** on its menu hides a folder's card; **Tools > Options** lists hidden folders, and
**Show again** then **Save** brings one back. A hidden folder whose file reads again is a project again. **↗ Open**, the first button on a project card that reads, a double-click,
**Enter** on it, **Open** on its menu, or **View > Open project**, fills the main area with the project: see
[The project view](project-view.md). A spec waiting for you shows **Waiting on you**, with
**View spec** and **Accept spec…**, which asks first and names the version it accepts; a spec changed after you accept
it waits for you again. **View spec**, wherever it appears, opens the [spec window](project-view.md#the-spec-window). **← Board** at the top, **Alt+Left**, or **View > Back to the board**, goes back.

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
| **PR 41 · ✓ CI passed ▾** | The card's pull request and CI, read from GitHub. For each of the card's repositories: the open pull request from its branch (draft or ready) and the newest CI run on that branch (running, passed, failed, or the run's own word, such as cancelled), with when. Click a row to open it on GitHub. With several repositories the button says how many and the worst of them. Theseus reads GitHub without a token, every five minutes or less often when there are many repositories, so it keeps within GitHub's 60 calls an hour; the status bar says when it last read, and its tooltip how many calls are left. What it does not know it says: **CI unknown** before the first read, when GitHub's limit is used up (with when it resets), or when GitHub does not show the repository; **CI not read** for a repository on another host, which Theseus does not read; **No CI run** when none of the repository's newest 100 runs is on the branch. Nothing is shown as passing unless GitHub said so. |
| on nigel/ledger | The git branch checked out. If the card's extra directories include repositories (a clone, a worktree, or a feature folder holding several), it is their branch: "on nigel/ledger · 3 repos" when they agree, **branches differ** in amber when they do not, with each repository's branch in the tooltip. Otherwise it is the branch of the card's working directory. |
| "shares … with …" in amber | Two workstreams share a handover document or a Jira issue. Two sessions writing the same file overwrite each other without either noticing, so give one its own document in its details. |
| "Restarted outside Theseus" | A session is running in this workstream's folder that the board is not following. **Adopt** it. |
