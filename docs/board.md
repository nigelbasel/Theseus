# The board

[Help home](index.md)

## Columns and rows

The board is a grid. **Columns are stages**: how far along a piece of work is. **Rows are areas**: what
kind of work it is. A new board starts with these, and you can change both in **Tools > Options**:

| Columns | Rows |
|---|---|
| New, Planning, In progress, Review, Done | Research & spikes, Dev, Support & admin, Side projects |

Drag a card to move it to another cell. Where a card sits is remembered.

## Paused and Blocked

Paused and Blocked are **marks on a card**, not columns, so a card keeps its stage while it waits.

- **Paused**: set aside on purpose.
- **Blocked**: waiting on something outside your control.

Set either from the card's **▾** menu or from its details. A marked card says so in amber under its
title. Starting a paused or blocked card asks first whether the mark still holds:

- **Yes** clears the mark and starts it.
- **No** leaves it marked and does not start it. When you are starting several cards, the rest still
  start.
- **Cancel** starts nothing.

## Folding

Anything can be folded, and Theseus remembers what you folded when it restarts.

- **A card**: the **⌃** button beside its status folds it to its title. The **⌄** opens it again. A
  folded card keeps its tick and its status dot, which is lit while its session is running.
- **A row**: the chevron beside the row's name. A folded row shows how many cards are in each stage,
  and **● 2 live** when sessions are running in it.
- **A column**: the **‹** beside the stage's name folds it to a narrow strip in every row. The strip
  shows its card count and **● n** for running sessions. **›** opens it again.

**View > Fold cards with no session** folds every card that is not running, leaving live work full
size. **View > Unfold all cards** and **View > Unfold all rows and columns** open everything again.

## The archive

**View > Archive** shows what has been put away instead of the working cards. Archiving takes a
workstream off the board without losing anything: its plan, notes and history stay.

## The menus, toolbar and status bar

Every command is in a menu. The ones you use most also have a toolbar button with the same icon.
Hover over a toolbar button to see its name and shortcut.

| Menu | What is in it |
|---|---|
| File | New workstream, Day report, Exit |
| Edit | Select all, Select live, Invert selection, Unselect all |
| View | The three panels, the archive, folding, the toolbar and status bar, Refresh |
| Workstream | Start, Stop and Archive the ticked cards |
| Window | Arrange the terminal windows |
| Tools | Options, and the logs and data folders |
| Help | These pages, Check for updates, What's new, About |

The toolbar's three panel buttons (live sessions, locks, chains) stay pressed while their panel is
open. Their count turns amber when something needs you: a lock held for over 90 minutes, a test
container nobody owns, or a chain waiting on you.

The **status bar** shows the latest notice at the left; click it to see earlier ones. On the right are
the card counts, plan usage, the locks in use, the account sessions run as, and the version.
