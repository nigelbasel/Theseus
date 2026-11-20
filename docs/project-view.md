# The project view

[Help home](index.md)

A project's view shows where the project is without asking any of its sessions. Open it by double-clicking the
project card, pressing Enter on it, choosing **Open** on its menu, or **View > Open project**. It fills the main area;
the drawer, the side panels and the status bar stay.

## Getting back and across

**← Board** at the top left, **Alt+Left**, or **View > Back to the board** goes back to the board. Next to it is a tab
for each project that reads; click one to open that project. **Esc** clears what is selected; it does not go back.

## Where the project is

Under the tabs are the project's title and one sentence saying where it is: how many pieces of work it has across how
many objectives, how many are done, busy, waiting on you, waiting for their holdout check, stuck or still to do, which
session is working on what, and what comes next. Six counts follow, zeros included.

While something waits on you, a band lists it: **Accept the spec of …** opens that piece of work, selects its spec and
asks you to accept it, naming the version; **Answer … in its terminal** brings that session's terminal forward.

## The marks

Every stage says where it is in a mark and a word, so nothing depends on colour alone:

| Mark | Word |
|---|---|
| ✓ | Done |
| ◆ | Waiting on you |
| ● | Busy |
| ⚠ | Stuck |
| ◐ | Holdouts not run yet |
| – | To do |

A stage is stuck only because of its records or the card's **Blocked** mark, never because time has passed. Busy and a
session waiting on you come from the session itself.

## The boxes

Time runs down the page. Each box is a **piece of work**: a row of the project's file, shown by its name, with the
workstream that works it, its kind (feature, bug or freeform), its objective, and a line saying where it is.

- **Folded** (▸, the first time you open a project), a box shows a strip of its stages, each with the first three
  letters of its name. Hover over one for its state.
- **Open** (▾), a box shows its stages as a chain, then what you can do: **Open spec** and **Accept spec…** while a
  spec waits on you, **Verify with holdouts…**, **⧉ Focus** the workstream's terminal, **▶ Start** or **■ Stop**, and
  **Open records**. Start, Stop, Focus and Verify act on the workstream's current piece of work only.

Right-click a stage, or press Shift+F10 or the Menu key on it, for **Properties** and that stage's own actions.

**Collapse all** folds every box, **Expand all** opens them all, and **Open what needs attention** opens what is busy,
waiting on you, stuck or waiting for its holdout check, and folds the rest. Each project keeps its boxes as you leave
them, across restarts. Nothing opens or folds by itself: a folded box that starts waiting on you stays folded and says
so in its status line and strip.

## The links

- A solid line with an arrow: one piece of work **hands work to** another, which sits indented under it. When it handed
  work to several at once, the bar beside them is thicker and says how many ran in parallel.
- A dotted line with an arrow: the same workstream **then worked on** its next piece of work.
- **↩ reported back to …** on a box: that workstream reported back to the piece it came from. A report that was lost is
  shown in the Stuck colour.

## The Properties pane

Click a box's name or status line to select the piece of work, or a stage to select the stage; from the keyboard, Tab
to a box's name or a stage and press Enter. The pane on the right shows its state in words, where its records are, its
workstream, its links with their state, and what you can do. For a piece of work in an objective, the pane also lists
the objective's criteria: click the ☐ or ☑ beside a criterion to tick or untick it in the project's file, and
**Edit objective…** to rename the objective.

Drag the splitter to resize the pane, or double-click it to put it back to its first width. The width is kept across
restarts and for every project.

## Keys

| Keys | Does |
|---|---|
| Alt+Left | Back to the board |
| Esc | Clear the selection |
| Tab | Moves through the chevrons, names, stages and buttons in reading order |
| Enter or Space on a name | Selects that piece of work |
| Shift+F10, or the Menu key | A stage's menu |
