# links-tree-scrollbar: fix

Pipeline: `bug` (method kit v0), stages **rule** and **fix**, autonomy L0, run overnight under Nigel's overnight
autonomy brief (2026-11-19). Branch `nigel/links-tree-scrollbar`.

## Spec (the rule)

Option A, **approved by Nigel, 2026-11-19** (taken first as overnight decision 1, then confirmed by him):

1. Nothing is ever drawn over a row of the Chains tree.
2. The tree never scrolls sideways: its own horizontal scrolling is off, and the panel scrolls only vertically, like
   the panel's other lists.
3. A card title that does not fit ends in "…"; its tooltip gives the full title above "Open this card's details".
4. The state on the right of a row ("Ready", "waiting") always shows; the title gives way, not the state.

## Cause

See `reproduction.md`: under the Fluent theme the tree's own horizontal scrollbar is laid over its bottom row.

## Change

- `MainWindow.xaml`, the Chains `TreeView`: `ScrollViewer.HorizontalScrollBarVisibility="Disabled"`, so the tree takes
  the panel's width and has no sideways scroll at all (rule 1, 2).
- The title button's content is a `TextBlock` with `TextTrimming="CharacterEllipsis"`, so it can shrink within the
  row's dock panel, where the state is docked right (rules 3, 4). Its tooltip gives the title, then "Open this card's
  details".
- **The tree's rows get their own item template** (overnight decision 2). The standard `TreeViewItem` lays its header
  out in an auto-width column, so a row was never held to the tree's width: with only the two changes above, the
  state was still pushed to x = 510 in a 318 panel (measured by `ChainsTreeTitleTests`). The new template stretches
  the header, keeps a chevron (a toggle on `IsExpanded`) for rows with children, and indents children 16 px. The
  standard selection highlight is not kept; nothing used it, since rows are buttons.

This addresses the cause (a scrollbar the tree should never have, and rows never held to its width) rather than the
symptom (moving the bar).

## Tests

- `ChainsTreeLayoutTests` (the reproduction): failed for the reported reason before the fix; passes after.
- `ChainsTreeTitleTests` (new file, P9): a long title trims inside the panel, and the state stays in the panel. Both
  failed after the first two changes, which is how the template problem was found, and pass after the third.
- Full suite 1553 of 1553.

## After verify (PASS, `evidence.md`)

| Finding | Disposition |
|---|---|
| Should fix 1: the row template's own behaviour untested | **Fixed.** `ChainsTreeRowTemplateTests` (new file): a chevron for a row with children, hidden but keeping its place on a row without, collapsing hides the children, children indented 16 px. Written after the template, so the review mutates them (P16). 1557 of 1557 |
| Should fix 2: the panel's "Ready" list has the same untrimmed button | **Left alone on purpose:** it has no scrollbar problem (a plain list in the panel's scroller), and the rule covers the tree. A long title there is cut at the edge with no "…". Question 3 for Nigel |
| Considers | The keyboard case is noted here: with the standard selection highlight gone, arrow keys show only the focus rectangle. `HorizontalAlignment="Stretch"` is the default and is kept for clarity. The tooltip's title line is a live check |

## Not reachable by the tests

How it looks on the real board (the chevron's look, spacing, the tooltip): Nigel's live look in the morning.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| rule | 18:55 | 18:56 | Nigel approved option A |
| fix | after the rule | this commit's time | |
