# links-tree-scrollbar: review

Pipeline: `bug` with a rule stage (method kit v0), stage **review**, autonomy L0, run overnight under the owner's
overnight autonomy brief. Reviewer: a fresh sub-agent that did not write the fix or verify it. Partial independence:
same model family as the implementer and the verifier, and it read `evidence.md` before writing this, so it is not
blind to the verifier's conclusions.

Method: code review only. It read `git diff origin/main...HEAD -- src tests`, `fix.md` (its Spec is the rule),
`reproduction.md`, `evidence.md`, `AGENTS.md` and `.agents/reviewer-rubric.md`. Nothing was built or run. Where a
finding depends on how WPF behaves at run time and not on this repo's code, it says so, and it says what would settle it.

Clock (reviewer's own, read from the machine): started 19:32:20, reading finished 19:35:18, this file written straight
after.

The brief named four test files under `tests/Theseus.Tests/Repository/ChainsTree*.cs`. The branch has three:
`ChainsTreeLayoutTests`, `ChainsTreeTitleTests` and `ChainsTreeRowTemplateTests`. All three are reviewed.

## Rubric

| Item | Verdict | Note |
|---|---|---|
| R1 Board changes pass the person in the tab | Not touched | |
| R2 A report is a claim | Not touched | |
| R3 Nothing starts a session on its own; the chains view is read-only | Holds | The new chevron sets only `IsExpanded` (MainWindow.xaml:1611). The title button still runs `Show`, which opens details |
| R4 Trackers read only; no durations from sessions | Not touched | |
| R5 to R8 Locks and clean-up | Not touched | |
| R9 to R14 Sessions | Not touched | |
| R15 Updates and secrets | Not touched | |
| R16 Conventional Windows behaviour | **Doubt**, see should fix 1 | A conventional tree shows which row the keyboard is on. The new template drops the selection highlight, and it may leave no visible focus at all |
| R17 Live state; setup dialogs | Not touched | |
| R18 A card does not grow without bound | Not touched | The Chains panel is not a card |
| R19 Lock colours | Not touched | |

AGENTS.md conventions:

| Convention | Verdict | Note |
|---|---|---|
| One type per file | Holds | Each test file has one public fixture. `Board` and `Chains` are private nested records, which the rule allows |
| Comments say why | Holds | Both XAML comments (MainWindow.xaml:1584-1587, 1591-1595) say why. The second has no date or decision, unlike the first; see consider 6 |
| Tests are sentences and prove one behaviour | Mostly holds | See should fix 3 and consider 1 |
| Test first for a bug | Holds | 518eb05 adds the failing reproduction before any source change (evidence.md checked this) |
| No warnings | Not judged | Nothing was built. evidence.md reports 0 warnings |
| Public repo | Holds | No machine paths, issue keys, internal system names or screenshots in the diff or the work files |

## Correctness against the rule

| Rule | Verdict by reading |
|---|---|
| 1. Nothing drawn over a row | Holds. With `HorizontalScrollBarVisibility="Disabled"` (MainWindow.xaml:1589) the tree's scroll viewer never shows a horizontal bar. The tree sits in a vertical `StackPanel`, so it is measured at unbounded height and never shows its own vertical bar either |
| 2. No sideways scrolling | Holds. The tree's scroller is Disabled, and the panel's scroller (MainWindow.xaml:1425) sets only the vertical bar; a `ScrollViewer`'s horizontal default is Disabled |
| 3. Title ends in "…", tooltip gives the full title above "Open this card's details" | Holds. `TextTrimming="CharacterEllipsis"` (1672), and the tooltip (1666-1671) is the title, semibold, then the fixed line. Trimming now works only because the row template's header column is `*` (1606) |
| 4. The state always shows; the title gives way | Holds. The state is docked right and comes first in the `DockPanel` (1650), so it is measured before the title button, which fills what is left |

The cause in `reproduction.md` is addressed, not the symptom. One point worth knowing about the second XAML comment
("the standard item lays its header out in an auto-width column"): the item style at 1597 has no `BasedOn`. The board
loads the Fluent dictionary through `ThemeMode`, which makes Fluent's styles implicit, and an explicit
`ItemContainerStyle` replaces an implicit style. So the "standard" item the fix replaced was most likely the classic
theme's, not Fluent's, both before and after this change. That bears on should fix 1 and on consider 4.

## What the new template loses

| Concern | Verdict by reading |
|---|---|
| Expand and collapse | Kept. The chevron's `IsChecked` binds two-way to `IsExpanded` (1611); the arrow keys and Left/Right live in `TreeViewItem`'s code, not its template |
| Screen reader, the tree | Kept. `TreeViewItemAutomationPeer` exposes expand and collapse from the item's code. The chevron is not focusable but stays in the automation tree with a name (1610) that matches what `Children` holds |
| Screen reader, the title button | **Name kept, help text likely lost.** The button's name now comes from its `TextBlock` content, which is the full untrimmed title, so a screen reader hears the whole title even when it is trimmed on screen. But the help text used to come from the string tooltip "Open this card's details". WPF reads help text from a tooltip only when it is a string (or an element whose plain text it can read), and a `StackPanel` gives none. See should fix 2 |
| Keyboard focus | **Doubt.** The selection highlight is gone (fix.md:31 says so). fix.md:49 and evidence.md say a focus rectangle remains. The focus visual in effect comes from the classic theme's `TreeViewItem` style, which, as this reviewer recalls it, is a rectangle with no stroke, because that theme shows focus through the selection highlight. If so, a keyboard user moving with the arrow keys sees nothing move. Not run; see should fix 1 |
| Light and dark | Kept. The chevron's colour is a `DynamicResource` Fluent brush (1616), so it follows a theme switch without a restart. The template paints no background of its own |
| High contrast | Kept for the chevron (the Fluent brush has a high-contrast value). Lost with the selection highlight: high-contrast users relied on it most |
| Disabled look, header padding, virtualisation | Lost, and they do not matter here: the tree is small and always enabled (as evidence.md says) |
| Mouse | Kept. A click on the title runs the button's command. A click on the chevron toggles. Double-click to expand lives in `TreeViewItem`'s code and is still there |

## Simplicity

A template is the right size of fix if the header has to stretch: neither `HorizontalContentAlignment` nor a width
binding on the row reaches the classic template's auto-width column, and a width binding to the tree's
`ActualWidth` would be more fragile than 40 lines of template. One cheaper option was not tried: `BasedOn` the
Fluent implicit `TreeViewItem` style, so the tree uses Fluent's own item template. If that template stretches its
header, the change shrinks to one attribute and keeps Fluent's selection and focus visuals, which would also settle
should fix 1. Whether it stretches cannot be judged by reading this repo. See consider 4.

## The tests

| Test | One behaviour? | Could it not fail? | Mutate |
|---|---|---|---|
| `ChainsTreeLayoutTests.ALongTitle_DoesNotCoverTheTreesBottomRow` | Yes | No: it failed on 518eb05 for the reported reason (evidence.md). Unchanged since | Already mutated (M1) |
| `ChainsTreeTitleTests.ALongTitle_IsTrimmedWithinThePanel` | Two assertions: trimming is set, and the title ends inside the panel | The ellipsis half is a markup check, not a layout check: an untrimmed `TextBlock` is clipped to the same width and passes the right-edge assertion. Only the property assertion caught M2 | Consider 1 |
| `ChainsTreeTitleTests.ALongTitle_LeavesTheStateShowingInsideThePanel` | Yes | **Yes, partly.** It checks that the state has some width and ends inside the panel, not that it shows in full. A state clipped to a few pixels passes | Swap the order of the state and the button in the `DockPanel`, or give the state a small `MaxWidth`; see should fix 3 |
| `ChainsTreeRowTemplateTests.ARowWithChildren_ShowsAChevron` | Yes | No: an inverted `HasItems` trigger fails it | Set the trigger's `Value` to True |
| `ChainsTreeRowTemplateTests.ARowWithoutChildren_HidesItsChevron_KeepingItsPlace` | Yes | No: M7 fails it. Collapsed instead of Hidden also fails it | Change Hidden to Collapsed (1636) |
| `ChainsTreeRowTemplateTests.CollapsingARow_HidesItsChildren` | Yes (the chevron reaching `IsExpanded`, and `IsExpanded` hiding the children, as one user action) | No: M5 and M6 each fail it. It asserts the mechanism (the `ItemsPresenter`'s `Visibility`), not the effect, so a template that hides children another way would fail it wrongly | M5 and M6; consider 2 |
| `ChainsTreeRowTemplateTests.AChild_IsIndentedUnderItsParent` | Yes | No: M8 fails it | Change the margin to 19 (the classic indent) |

These were written after the template (fix.md:47), so P16 asks for them to be mutated. By reading, M5 to M8 in
evidence.md's table would now each be caught. The reviewer would want those four mutations re-run, plus the two above.
Still uncovered: the chevron's glyph swap (1620-1622) and the tooltip's title line (M9); see consider 3.

**Duplicated helper code.** `PanelMarkup`, `Converter`, `Descendants`, `RepositoryRoot`, the layout steps and the
`Board`/`Chains` records are repeated in all three files, about 60 lines each. It has already drifted:
`ChainsTreeLayoutTests` has a different `Converter` signature (line 101) from the other two (lines 113 and 88). A shared
helper is allowed by the one-type-per-file rule: one file, say `ChainsPanelLayout.cs`, holding a static class with
`Board` and `Chains` as its private nested records. Against it: the repo's existing tests duplicate small helpers
rather than share them (the three `LockGuard*Tests` files each have their own `Guard` and `RepositoryRoot`), and a
missed edit fails loudly, as a XAML parse error, not silently. `ChainsTreeLayoutTests` should stay byte-identical
anyway, since the evidence relies on the reproduction being unchanged. So: worth it for the two newer files when a
fourth such test is added, not a reason to hold this change. Consider 5.

## Findings

### Must fix

None.

### Should fix

1. **Keyboard focus may be invisible in the tree.** MainWindow.xaml:1597-1640; rubric R16. The template drops the
   selection highlight, and the focus visual left in effect may draw nothing (see "What the new template loses").
   fix.md:49 and evidence.md say a focus rectangle remains; neither ran it, and this reviewer's reading says
   otherwise. The live check settles it: Tab into the tree and press Down. If no row visibly changes, add a trigger
   on `IsSelected` (or `IsKeyboardFocused`) in the template that paints the header with a theme brush, so it works
   in light, dark and high contrast. If a rectangle does show, correct nothing, but say so in fix.md. If the
   highlight is not restored, fix.md:31 ("nothing used it") should mention the keyboard case. Line 49 already does.
2. **The title button has probably lost its help text for screen readers.** MainWindow.xaml:1666-1671. With a string
   tooltip, WPF exposed "Open this card's details" as the button's help text; with a `StackPanel` tooltip it most
   likely exposes none. Adding `AutomationProperties.HelpText="Open this card's details"` to the button (1662) restores
   it at no cost. A test can read it through `UIElementAutomationPeer.CreatePeerForElement(button).GetHelpText()`
   without a window. This depends on WPF's automation peer, not on this repo, so the test is the check.
3. **Rule 4's test does not prove the state shows in full.** ChainsTreeTitleTests.cs:43-45. Rule 4 says the state
   "always shows". The test asserts `ActualWidth > 0` and that the state ends inside the panel. Asserting that the
   state's `ActualWidth` is at least its `DesiredSize.Width` (it was not clipped) would catch a state squeezed to a
   sliver. Mutate by swapping the two `DockPanel` children (1650 and 1662) and check that the test fails.

### Consider

1. ChainsTreeTitleTests.cs:31: the ellipsis is proven by reading `TextTrimming` back, which is a markup check (the
   fixture's summary cites P11, checked against what is laid out). A layout-visible check: a trimmed `TextBlock`'s
   `DesiredSize.Width` stays within what it was given, while an untrimmed one asks for the full title's width. That
   would also make the test one behaviour: "the title asks for no more than the panel gives it".
2. ChainsTreeRowTemplateTests.cs:50: assert the effect (the tree's height falls, or the child row's `ActualHeight` is
   0) rather than the `ItemsPresenter`'s `Visibility`, so the test survives a different way of hiding children.
3. Nothing tests the chevron's glyph swap (MainWindow.xaml:1620-1622). If it broke, an expanded row would point
   right, and a user would read it as collapsed. One assertion in `CollapsingARow_HidesItsChildren`'s style would
   cover it. The tooltip title line (M9) remains for the live check.
4. Before keeping the template, try `BasedOn="{StaticResource {x:Type TreeViewItem}}"` on the item style (1597) under
   the Fluent theme. If Fluent's own item template stretches the header, the change is smaller and keeps Fluent's
   selection and focus look. If it does not, record that in fix.md, so the template's reason is complete.
5. Shared test helper for the two newer files (see "The tests"). Not needed now.
6. MainWindow.xaml:1591-1595: the second comment says why but, unlike the first, cites no decision. "(overnight
   decision 2, 2026-11-19)" would match the convention. Its words "stretches the header" are true of the `*` column
   (1606), not of `HorizontalAlignment="Stretch"` (1627), which is the default (evidence.md, M3b).
7. The chevron's hit target is small: 16 px wide and an 8 pt glyph (1608-1617). The live check should note whether
   it is easy to hit. The panel's own "Hide the chains" button also uses ChevronRight (E76C, line 1420), so a
   collapsed row's chevron and the hide button look alike. That is minor, but a user could misread it.
8. Already with the owner (fix.md question 3): the READY list above the tree still clips long titles with no "…".
   A user sees the same title trimmed in one list and cut off in the other. Not a finding against this change.

## Summary

The change meets the four rules by reading, addresses the cause in `reproduction.md` rather than the symptom, and
touches no rubric invariant. The template is a reasonable size for what it has to do. There is nothing that must be
fixed. It can be accepted, subject to one condition: the owner's live check confirms that the keyboard focus is
visible in the tree (should fix 1). If it is not, the template should paint the focused or selected row before this
merges, because otherwise arrow-key navigation shows nothing. Should fix 2 (help text) is a one-attribute change with
a cheap test. Should fix 3 tightens a test that can pass with the state clipped. The duplicated test helpers are not
worth a shared helper yet.

## What was done with each finding (overnight, under Nigel's autonomy brief, 2026-11-19)

| Finding | Disposition |
|---|---|
| Should fix 1: keyboard focus may be invisible | **Fixed.** The row the arrow keys are on (the selected row) is highlighted with Windows 11's own list-selection brush, `SubtleFillColorSecondaryBrush`. Tests `TheSelectedRow_IsHighlighted` and `AnUnselectedRow_IsNotHighlighted` (new file `ChainsTreeAccessibilityTests`). That test host loads Fluent's light colours as well as its styles: without them the brush resolved to nothing and the test could not tell a working trigger from a broken one |
| Should fix 2: the button lost its screen-reader help text | **Fixed.** `AutomationProperties.HelpText="Open this card's details"`; test `TheTitleButton_TellsAScreenReaderWhatItDoes` reads it through the automation peer |
| Should fix 3: rule 4's test let a squeezed state pass | **Fixed** with a new test, `ALongTitle_LeavesTheStateItsFullWidth` (the existing one is unchanged): the state's actual width equals its wanted width less its margin (35.3 of 35.3). Its first version failed because it compared against a desired size that includes the 8 px margin; corrected before the commit |
| Considers | Recorded. The `BasedOn` Fluent alternative was not tried: the custom template is tested and keeps what the board uses. The duplicated test helpers stay until a fourth test of this kind, as the review suggests. The "Ready" list is question 3 |

Full suite 1561 of 1561.
