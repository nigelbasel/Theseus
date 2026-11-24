# The project view

[Help home](index.md)

A project's view shows where the project is without asking any of its sessions. Open it by double-clicking the
project card, pressing Enter on it, choosing **Open** on its menu, or **View > Open project**. It fills the main area;
the drawer, the side panels and the status bar stay.

## Getting back and across

**← Board** at the top left, **Alt+Left**, or **View > Back to the board** goes back to the board. Next to it is a tab
for each project that reads, its stacked-card mark in the project's own colour; click one to open that project. **Esc** clears what is selected; it does not go back.

## Where the project is

Under the tabs are the project's mark in its colour, its title and the word PROJECT, as on its card on the board, then
one sentence saying where it is: how many pieces of work it has across how
many objectives, how many are done, busy, waiting on you, waiting for their holdout check, stuck or still to do, which
session is working on what, and what comes next. Six counts follow, zeros included. The project card shows the same
sentence and the same counts, leaving out the zeros. The colour is changed in **Project properties…** on the project
card's menu.

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
  spec waits on you, **Independent check…**, **⧉ Focus** the workstream's terminal, **▶ Start** or **■ Stop**, and
  **Open records**. Start, Stop, Focus and Verify act on the workstream's current piece of work only.

Right-click a stage, or press Shift+F10 or the Menu key on it, for **Properties** and that stage's own actions.

Three small icons right of **Group by**, each named in its tooltip: **Collapse all** folds every box, **Expand all**
opens them all, and **Open what needs attention** opens what is busy,
waiting on you, stuck or waiting for its holdout check, and folds the rest. Grouped by objectives, they fold and open
the headings too, and Open what needs attention opens the headings on the way to what needs you. Each project keeps
its boxes and headings as you leave them, across restarts. Nothing opens or folds by itself: a folded box that starts
waiting on you stays folded and says so in its status line and strip.

## The spec window

**Open spec**, from a box, the board or the menus, opens the spec in its own window, which can be resized and opens
where you last left it. It is titled with the piece of work and the spec's version. **Preview** shows the spec drawn,
**Source** its Markdown as written; **Ctrl+F** finds in it, and **Enter** or **F3** finds the next match. **Accept spec…** at its
foot accepts the version on screen, after the same question, which names the card Theseus then tells and says whether it
will be started; it is disabled, saying why, once that version is accepted. Accepting tells the card: a running session
with its next turn, a stopped card started and told to carry on to the plan. When you say "accept" in the session's tab
instead, the session reports it, and the box shows **You accepted this in <card>'s tab: Confirm…**: only your confirmation
on the board accepts it. **Properties** shows the record: the version, when, by whom, and how ("on the board", or "said in
<card>'s tab, confirmed on the board").
The window follows its file: a spec changed on disk is drawn again and says so, and one changed since it was drawn is
drawn again before it can be accepted. **Open in editor** opens the file itself; **Close** or **Esc** closes the window.

A session in a project creates a new workstream naming the objective it serves, its records folder, its branch and its
repos; the card is made at once and its row written under that objective, with branch `none` and repos `read-only` when
it names none. One naming no objective, or one the file does not have, is refused and nothing is made. A session's start
of a card you paused waits in **Waits for you** with **Allow or refuse…**. A verifier card's row
is written too, under the checked card's objective, its records folder named verify- and the checked one's.

## Writing prompts

The prompt boxes (a new workstream's prompt, Setup's two, and Settings' identity, standing, continue and stop prompts)
and Add objective's criteria take Markdown. The bar above each inserts it: **B** bold (**Ctrl+B**), **I** italic
(**Ctrl+I**), **H** a heading, a bulleted list, a numbered list, a check box, code, and a link (**Ctrl+K**). Bold,
italic, inline code and the line buttons take their Markdown off again when pressed on it. **Write | Preview** shows the text drawn. What is saved is exactly what is
typed; the bar only types Markdown for you.

## Group by

The two tabs over the page, **Rounds and work | Objectives only** (the arrow keys move between them), or **View > Group project by**, choose
how the boxes are laid out.
Each project keeps its own choice, across restarts.

- **Rounds and work** shows the pieces of work as they hand work on, time running down the page, with the links below.
- **Objectives only** shows every phase of the project's file as a heading, in file order, with its exit gate and how many
  of its objectives are done. Under each phase, every objective is a heading: its number and title, how many of its
  criteria are ticked (such as 0/6), and the counts of its pieces of work; one that nothing works on yet says
  **nothing yet**. Under each objective are its pieces of work, as the same boxes, with the same stages, buttons and
  Properties. Pieces linked into the project but listed in no project file come last, under **Not in project.md**;
  each of their boxes, and its Properties, offers **Add to project…**, which opens the board's window with this project
  chosen, and once added the box moves under its objective.

Headings fold and open with their chevron, like a branch; a phase whose objectives are all done starts folded. No line
is drawn between headings, so a box says what the lines would: **← handed on from …**, **→ handed on to …**, and,
where the same workstream moved on to another objective, **┈ worked on after …** and **┈ then worked on …**.

Click an objective heading to show it in Properties: its pieces of work, its criteria to tick with ☐ or ☑, and **Rename
objective…**, which changes its title. Click a phase heading to see its exit gate and objectives, with **Add objective…**, which adds one at the
end of that phase. Right-click a heading, or press Shift+F10 or the Menu key on it, for the same.

## The links

- A solid line with an arrow: one piece of work **hands work to** another, which sits indented under it. When it handed
  work to several at once, a brace to the left of the line groups them all, from the first to the last, with how many ran
  in parallel beside it; no line crosses it.
- A dotted line with an arrow: the same workstream **then worked on** its next piece of work.
- **↩ reported back to …** on a box: that workstream reported back to the piece it came from. A report that was lost is
  shown in the Stuck colour.

A button that cannot act now is shown disabled, and hovering over it says why, such as "No holdout scenarios for this
project yet". Where there is a way on, it is offered beside the button as a link, such as **Write scenarios…** or **Set the
holdout folder…**.

## The Properties pane

Click a box's name or status line to select the piece of work, or a stage to select the stage; from the keyboard, Tab
to a box's name or a stage and press Enter. The pane on the right shows its state in words, where its records are, its
workstream, its links with their state, and what you can do. For a piece of work in an objective, the pane also lists
the objective's criteria: click the ☐ or ☑ beside a criterion to tick or untick it in the project's file, and
**Rename objective…** to change the objective's title. Its criteria are edited in project.md itself.

Drag the splitter to resize the pane, or double-click it to put it back to its first width. The width is kept across
restarts and for every project.

When the window is too narrow for the boxes and the pane side by side (such as the narrowest window with the Live
sessions panel open), the pane steps aside: the boxes take the whole width, and the status bar says **Properties
hidden: the window is too narrow**. Selecting something then opens Properties over the right of the view; close it with
**×** or Esc, and what you selected stays selected. Make the window wider and the pane comes back beside the boxes, at
the width you left it.

## Keys

| Keys | Does |
|---|---|
| Alt+Left | Back to the board |
| Esc | Closes Properties when it is open over the view; otherwise clears the selection |
| Tab | Moves through the chevrons, names, headings, stages and buttons in reading order |
| Enter or Space on a name or heading | Selects that piece of work or heading |
| Shift+F10, or the Menu key | A stage's or a heading's menu |
