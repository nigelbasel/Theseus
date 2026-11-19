# links-tree-scrollbar: evidence

Pipeline: `bug` (method kit v0), stage **verify**. Verifier: an independent sub-agent that did not write the fix.
Partial independence: same model family as the implementer, and it was given the contracts only. No holdout scenario
covers this behaviour, so none was run.

Clock (verifier's own, read from the machine): started 18:58:27, measurements finished 19:27:57, this file written
straight after.

## Results

| Check | Result |
|---|---|
| `dotnet test Theseus.slnx --nologo` in the feature worktree | **1553 passed, 0 failed, 0 skipped** (2 m 13 s). Build: 0 warnings, 0 errors. The only extra console lines are three "launch ticket" messages that tests print on purpose |
| Copy at `<scratch>/copy` (no bin/obj), fixed XAML, full test project | 1553 of 1553 (baseline for the mutations) |
| Reproduction test unchanged since 518eb05 | `git diff 518eb05 HEAD` on `ChainsTreeLayoutTests.cs` is empty; byte comparison identical |
| 518eb05 changed no source | `git diff origin/main 518eb05 -- src/` is empty, so 518eb05's `MainWindow.xaml` is the unfixed board |

## Reproduction check

In the copy, `src/Theseus.App/MainWindow.xaml` replaced by its 518eb05 version, and the ChainsTree tests run:

```
tree 318x65.9; viewport 318x65.9; extent 510.2x65.9; horizontal bar Visible; style ScrollViewer; last row ends at 65.9
  bar Horizontal Visible 318x12 at y 53.9
Failed ALongTitle_DoesNotCoverTheTreesBottomRow
  a horizontal scrollbar is drawn over the bottom row (row ends at 65.9)
Failed ALongTitle_IsTrimmedWithinThePanel
Failed ALongTitle_LeavesTheStateShowingInsideThePanel
  the state is not pushed off the panel's edge
```

This is the reported failure, word for word as in `reproduction.md`: a 12 px horizontal bar at y 53.9 over a row that
ends at 65.9. With the fixed XAML all three pass.

## Rule table

| Rule | How judged | Verdict |
|---|---|---|
| 1. Nothing is ever drawn over a row | `ALongTitle_DoesNotCoverTheTreesBottomRow` passes. A probe in the copy found both horizontal bars Collapsed, 0x0 | Met |
| 2. No sideways scrolling; the panel scrolls only vertically | XAML: the tree has `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` (MainWindow.xaml:1589). The panel's scroll viewer sets only `VerticalScrollBarVisibility="Auto"`, and a ScrollViewer's horizontal default is Disabled. Probe: the panel's scroller and the tree's scroller are both H Disabled/Collapsed, tree extent 313.8 in a 318 viewport | Met |
| 3. A long title ends in "…"; the tooltip gives the full title above "Open this card's details" | Trimming: `ALongTitle_IsTrimmedWithinThePanel` passes for a root. A probe found an indented child's long title also ends inside the panel (right edge 253.9). Tooltip: XAML (MainWindow.xaml:1666-1671) has a SemiBold `{Binding Title}` line above "Open this card's details". The tooltip's text is not tested; see live check 3 | Met by test and reading; tooltip text needs the live check |
| 4. The state always shows; the title gives way | `ALongTitle_LeavesTheStateShowingInsideThePanel` passes for a root. Probe: a child row's state lies at 264.8 to 312, inside 318 | Met |

## Template review (MainWindow.xaml:1599-1643)

Checked by reading and by a throwaway probe test that exists only in the copy (`VerifierProbeTests`, under the Fluent
theme, at the panel's width):

- **Expand and collapse work.** The chevron's `IsChecked` is bound to the item's `IsExpanded`, and `IsChecked` binds
  two-way by default. The probe toggled the chevron through its automation peer: `IsExpanded` False, children
  Collapsed, chevron E76C (pointing right), and the tree's height fell from 65.9 to 43.9. Toggling again gave True,
  Visible and E70D (pointing down). Setting `IsExpanded=false` on the item unchecks the chevron. The open state lasts
  until the tree's shape changes: `ChainsViewModel.Update` rebuilds only when a title, state or link changes, and a
  rebuild shows everything expanded again. That was already true before this fix.
- **Children are indented.** A child starts 16 px to the right of its parent. Item widths are 318 for the parent and
  302 for the child.
- **The chevron hides on rows without children.** The `HasItems=False` trigger sets it to Hidden, not Collapsed. A
  leaf's title therefore starts at the same x (26) as a parent's, and titles stay aligned.
- **Keyboard and focus.** The chevron is not focusable, as in the standard template. The item is still focusable, and
  arrow-key moves and Left/Right expand or collapse live in `TreeViewItem`'s own code, so the template does not change
  them. A focus visual style is still applied: the item style has no `BasedOn`, but the theme style's setters still
  apply. What is lost is the **selection highlight**. A keyboard user moving with the arrow keys now sees only the
  dashed focus rectangle, which surrounds the whole item including its children. fix.md says nothing used the
  highlight; no code reads the tree's selection (checked: no `SelectedItem` or `TreeViewItem` use in `src/`), so that
  holds for mouse use.
- **Also lost from the standard template:** the disabled foreground, the header's padding box, and the
  `VirtualizingStackPanel` arrangement. None of these matters for a small, always-enabled tree.
- **The "Ready" list** (MainWindow.xaml:1561-1575) still has the old button markup: `Content="{Binding Title}"`, no
  trimming, and a tooltip of only "Open this card's details". fix.md does not say it was left alone on purpose; it
  does not mention it at all. It does **not** have the overlay problem: it is a plain `ItemsControl` with no scroll
  viewer of its own, inside the panel's scroller, whose horizontal scrolling is Disabled. The probe saw no horizontal
  bar, and the button was held to 298 px (right edge 310 of 318). But a long title there is **cut off at the edge with
  no "…"**, and its tooltip does not give the title. The same flaw rules 3 and 4 fix in the tree is still there in the
  list just above it.

## Text check (P17)

- Tooltip: the title, then "Open this card's details". This matches rule 3's wording exactly.
- Chevron's automation name: "Show or hide the cards this one hands over to". This matches what `Children` holds
  (`ChainNodeViewModel.Children`: "The cards it hands over to").
- First comment (above the TreeView): the bar under the Fluent theme was drawn over the bottom row, and a long title
  now trims. This is true: it matches the reproduction and rules 1 to 3.
- Second comment: "The standard item lays its header out in an auto-width column". This matches the measured
  behaviour (state pushed to x 510 without the template; mutant M4 fails the same two tests). The rest of the comment
  ("stretches the header, keeps a chevron for rows with children, and indents children") matches the template and the
  probe.
- fix.md's "indents children 16 px" matches what was measured (16). Its "Full suite 1553 of 1553" matches this run.
  Its "nothing used [the selection highlight]" is true of the code, but it leaves out the keyboard case above.
- One thing the tests cannot see: the probe read the tooltip's title line as empty, because a tooltip's content
  takes its DataContext only when it opens. That is expected in WPF and is not a fault, but it means no test reads
  the tooltip's title. Live check 3 covers it.

## Mutation table (P16)

Each part was removed in the copy only, and the full test project (1553 tests) was run against it.

| # | Part removed | Tests that fail | Survives? |
|---|---|---|---|
| M1 | `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` | DoesNotCoverTheTreesBottomRow, IsTrimmedWithinThePanel, LeavesTheStateShowingInsideThePanel | Caught |
| M2 | `TextTrimming="CharacterEllipsis"` | IsTrimmedWithinThePanel | Caught |
| M3a | the header column's `Width="*"` (changed to `Auto`) | IsTrimmedWithinThePanel, LeavesTheStateShowingInsideThePanel | Caught |
| M3b | the ContentPresenter's `HorizontalAlignment="Stretch"` | none | **Survives. Equivalent:** Stretch is the default, so the attribute does nothing |
| M4 | the whole item template (the `Template` setter) | IsTrimmedWithinThePanel, LeavesTheStateShowingInsideThePanel | Caught |
| M5 | the chevron's `IsChecked` binding to `IsExpanded` | none | **Survives**: no test covers expand and collapse |
| M6 | the `IsExpanded=False` trigger that hides children | none | **Survives** |
| M7 | the `HasItems=False` trigger that hides the chevron | none | **Survives** |
| M8 | the 16 px indent of children | none | **Survives** |
| M9 | the tooltip's title line | none | **Survives** |

M1 to M4, the parts the rule depends on, are each caught. M5 to M9 are what the new template adds on top: behaviour
the standard template used to provide, and the tooltip. Nothing tests them. The probe shows them working today, but a
later edit could break them silently.

## Findings

**Must fix:** none.

**Should fix:**
1. The template's expand and collapse, the hidden chevron on rows without children, and the indent are untested
   (M5 to M8 survive), MainWindow.xaml:1608-1637. The fix replaced standard behaviour with hand-written markup. A test
   in the style of ChainsTreeTitleTests should toggle the chevron and assert that the children collapse, that a leaf's
   chevron is hidden, and that a child sits 16 px in. The verifier's probe shows this can be done without a window.
2. The "Ready" list (MainWindow.xaml:1568-1570) still clips a long title with no "…", and its tooltip has no title.
   It has no overlay. fix.md should either say it was left alone on purpose, or apply the same `TextBlock` with
   trimming and the same two-line tooltip. The owner decides: the rule as written covers only the Chains tree.

**Consider:**
1. No test reads the tooltip's title line (M9), MainWindow.xaml:1666-1671. Live check 3 covers it.
2. fix.md:31, "nothing used it": add that the arrow keys no longer show a highlight on the row they move to; only
   the focus rectangle remains.
3. `HorizontalAlignment="Stretch"` on the ContentPresenter (MainWindow.xaml:1626) is the default. It is harmless,
   but no test can tell it is there.

## Live-check steps for the owner (checked against the code; not run)

1. **A card with a long title in the Chains panel.** Give a card that has a link a title much longer than the panel
   (the panel is 340 wide). Open the Chains panel. Expected: the row's title ends in "…" before the state. The state
   ("Ready" in green, or "⧗ waiting" in grey) shows in full at the right edge. There is no horizontal scrollbar
   anywhere in the panel, and the bottom row of the tree is fully readable. A long title in a child row trims the same
   way, 16 px further in. If the card is Ready, it also appears in the READY list above: there its title is
   **expected to be cut off at the edge with no "…"** (should-fix 2), with no scrollbar.
2. **Expanding and collapsing a chain.** A row with follow-on cards shows a small down-pointing chevron at its left;
   a row without them shows none, and its title is still aligned with the others. Click the chevron. Expected: the
   chevron points right, the child rows disappear, and the rows below move up. Click it again: the children return
   and the chevron points down. Clicking the title still opens the card's details, not the toggle. The tree goes back
   to fully expanded when any card in a chain changes title, state or link (the tree is rebuilt), and that is
   expected. With the keyboard, Tab into the tree and use Up/Down: a dashed focus rectangle moves between rows, and
   there is **no** coloured selection highlight. Left and Right collapse and expand the focused row.
3. **The tooltip.** Hover over a truncated title in the tree. Expected: a tooltip with the full title in semibold on
   the first line, and "Open this card's details" below it. Hover over a title in the READY list: its tooltip is only
   "Open this card's details".

## Notes on the verifier's own conduct

- A first copy of the full-suite console output was written to `<scratch>` outside the copy folder, before the
  verifier noticed the rule that only the copy folder may be written to. It was not deleted (no deletes). Everything
  else was written in `<scratch>/copy`: the mutation script, the per-mutant outputs and the probe test. The copy's
  `MainWindow.xaml` was restored to the fixed version afterwards.

## Verdict

**PASS.** The build is clean and the full suite is green. The reproduction test is unchanged, failed on 518eb05's
code for the reported reason, and passes now. Rules 1 to 4 are met. Every part the rule depends on is caught by a
test. Two should-fix findings remain, neither of them a regression: tests for the template's added behaviour, and a
decision on the READY list.
