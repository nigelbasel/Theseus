# links-tree-scrollbar: verify of the review fixes

Pipeline: `bug` with a rule stage (method kit v0), stage **re-verify of the review fixes** (70ef361 and 6fa9896).
Verifier: an independent sub-agent that wrote neither the fix, the first verify, the review nor the review fixes.
Partial independence: same model family as all of them, and it read `fix.md`, `evidence.md` and `review.md` (with
their conclusions) before checking. No holdout scenario was run.

Clock (verifier's own, read from the machine): started 19:45:08, measurements finished 20:09:52, this file written
straight after (20:10).

## Results

| Check | Result |
|---|---|
| `dotnet test Theseus.slnx --nologo` in the feature worktree | **1561 passed, 0 failed, 0 skipped** (1 m 59 s). Extra console lines: only the three "launch ticket" messages tests print on purpose |
| `dotnet build Theseus.slnx --no-incremental` | 0 warnings, 0 errors |
| Reproduction test unchanged | `git diff 518eb05 HEAD` on `ChainsTreeLayoutTests.cs` is empty; SHA-256 of the 518eb05 blob and of the working file are identical |
| Copy (no bin/obj/.git), full test project, plus the verifier's probe file | 1566 of 1566 (1561 plus 5 probe tests), the baseline for the mutations |
| Worktree after the run | Clean (`git status` empty) apart from this file |

## The three should-fixes

### 1. The selected row is highlighted (MainWindow.xaml:1603, 1635-1638)

By reading: a `Border` named `Highlight` wraps only the row's header grid (chevron and content), not the
`ItemsPresenter`, so a selected parent's children are not painted. The trigger is on `IsSelected`, so it follows the
arrow keys (which select the row they move to) and stays on the last row when the tree loses focus.

**Dark theme.** The brush is a `DynamicResource`, so it follows the theme. A probe (a copy of
`ChainsTreeAccessibilityTests` loading `Fluent.Dark.xaml` instead of the light colours, in the copy only) passes all
three tests; the selected row's highlight is `#0FFFFFFF`. The shipped tests only load the light colours.

**Is it visible?** Values read from Fluent's own dictionaries by a probe:

| Brush | Light | Dark | High contrast dictionary |
|---|---|---|---|
| `SubtleFillColorSecondaryBrush` (the highlight) | `#09000000` | `#0FFFFFFF` | `#FFF0F0F0` |
| `LayerFillColorDefaultBrush` (the Chains panel) | `#80FFFFFF` | `#4C3A3A3A` | `#FFFFFFFF` |
| `ApplicationBackgroundBrush` (the window) | `#FFFAFAFA` | `#FF202020` | `#FFFFFFFF` |
| `AccentFillColorDefaultBrush` | `#FF0067C0` | `#FF4CC2FF` | `#FF0067C0` |

Composited (window, then panel, then highlight): in light the panel is about RGB 252 and a highlighted row about
244, a contrast of about **1.08:1**; in dark the panel is about 40 and the row about 52, about **1.19:1**. In the
high-contrast dictionary it is `#F0F0F0` on white, about 1.13:1. It is a faint grey band, not something a keyboard
user picks out at a glance, and far below the 3:1 usually asked of a state indicator.

The commit calls this "Windows 11's own list-selection brush". A probe of Fluent's own selected `TreeViewItem`, in
light and dark, shows that is half of it: Fluent paints the same `SubtleFillColorSecondaryBrush` fill **and** a
3 x 16 px `ActiveRectangle` in `AccentFillColorDefaultBrush` at the row's left. The accent bar is what makes Fluent's
selection visible; the fix kept the fill and left out the bar. The board's own cards also show selection with
`AccentFillColorDefaultBrush` (MainWindow.xaml:438-441). See should fix 1 below.

Test: `TheSelectedRow_IsHighlighted` asserts the background is neither null nor Transparent. It can fail (M1), but it
would pass any brush, however faint.

### 2. Screen-reader help text (MainWindow.xaml:1672)

`AutomationProperties.HelpText="Open this card's details"` is on the title button. `TheTitleButton_TellsAScreenReaderWhatItDoes`
reads it through `UIElementAutomationPeer`, and fails when the attribute is removed (M2). Fixed.

### 3. The state's full width (ChainsTreeTitleTests.cs:48-58)

**Not fixed by the new test.** `ALongTitle_LeavesTheStateItsFullWidth` compares the state's `ActualWidth` with its
`DesiredSize` less its margin. Both are the wrong measures for "not squeezed":

- `DesiredSize` is measured against the space the `DockPanel` offers, so it shrinks with the squeeze. With the state
  and the button swapped (M8, the very mutation the review named) it read `actual 35.3, wanted -3.65` and passed.
- A `TextBlock`'s `ActualWidth` is its render size, which is not reduced by clipping. With a `MaxWidth="10"` added to
  the state (an extra mutant, M9) it read `actual 35.3, wanted 10` and passed.

Both squeezes are caught, but only by the older `ALongTitle_LeavesTheStateShowingInsideThePanel`. So rule 4 is still
guarded, but the new test cannot fail and its message ("a sliver would pass the other test") is not true of it. See
should fix 2.

## The new tests: could each fail?

| Test | Fails under | Verdict |
|---|---|---|
| `ChainsTreeRowTemplateTests.ARowWithChildren_ShowsAChevron` | M3 | Can fail |
| `ChainsTreeRowTemplateTests.ARowWithoutChildren_HidesItsChevron_KeepingItsPlace` | M3, M4 | Can fail |
| `ChainsTreeRowTemplateTests.CollapsingARow_HidesItsChildren` | M6, M7 | Can fail |
| `ChainsTreeRowTemplateTests.AChild_IsIndentedUnderItsParent` | M5 | Can fail |
| `ChainsTreeAccessibilityTests.TheSelectedRow_IsHighlighted` | M1 | Can fail; accepts any non-transparent brush |
| `ChainsTreeAccessibilityTests.AnUnselectedRow_IsNotHighlighted` | none of the eight | A guard against a highlight that is always on; no mutant here targets it. By reading, it fails if the `Border`'s base `Background` were set to the fill |
| `ChainsTreeAccessibilityTests.TheTitleButton_TellsAScreenReaderWhatItDoes` | M2 | Can fail |
| `ChainsTreeTitleTests.ALongTitle_LeavesTheStateItsFullWidth` | none (passes M8 and M9) | **Cannot fail on a squeezed state** (should fix 2) |

## Mutation table (P16)

Each change was made to `MainWindow.xaml` in the copy only, and the copy's full test project (1566 tests: 1561 plus
the verifier's 5 probe tests) was run against it. The copy's file was restored and compared byte for byte afterwards.
Where two failures are listed for one test name, the second is the probe's dark-theme copy of that test.

| # | Change | Tests that fail | Survives? |
|---|---|---|---|
| M1 | remove the `IsSelected` trigger | `TheSelectedRow_IsHighlighted` (light; and the probe's dark copy) | Caught |
| M2 | remove `AutomationProperties.HelpText` | `TheTitleButton_TellsAScreenReaderWhatItDoes` (light; and the probe's dark copy) | Caught |
| M3 | `HasItems` trigger's `Value` False to True | `ARowWithChildren_ShowsAChevron`, `ARowWithoutChildren_HidesItsChevron_KeepingItsPlace` | Caught |
| M4 | the chevron's `Hidden` to `Collapsed` | `ARowWithoutChildren_HidesItsChevron_KeepingItsPlace` | Caught |
| M5 | the children's indent 16 to 19 | `AChild_IsIndentedUnderItsParent` | Caught |
| M6 | remove the `IsExpanded=False` trigger | `CollapsingARow_HidesItsChildren` | Caught |
| M7 | unbind the chevron's `IsChecked` from `IsExpanded` | `CollapsingARow_HidesItsChildren` | Caught |
| M8 | swap the state and the button in the row's `DockPanel` | `ALongTitle_LeavesTheStateShowingInsideThePanel` only | Caught, **but not by the new full-width test** |
| M9 (extra) | `MaxWidth="10"` on the state (run against the `ChainsTree` tests only) | `ALongTitle_LeavesTheStateShowingInsideThePanel` only | Caught, **but not by the new full-width test** |

No mutant survives the suite. The one gap is a test that survives every mutant meant for it.

## Findings

### Must fix

None.

### Should fix

1. **The selection highlight is too faint to do its job.** MainWindow.xaml:1635-1638. The fill alone is about 1.08:1
   against the panel in light and 1.19:1 in dark. Fluent's own selected tree row pairs this same fill with a
   3 x 16 px accent bar (`AccentFillColorDefaultBrush`), and that bar is what a user sees. Add the bar (for example a
   `Rectangle` in the chevron column, collapsed by default, made visible by the same `IsSelected` trigger), or use the
   accent brush for the highlight as the board's cards do (MainWindow.xaml:438-441). Note the row already has a 4 px
   card-colour stripe at its left (MainWindow.xaml:1653), so where the bar goes is a look the owner should judge live.
   The commit's "Windows 11's own list-selection brush" should say it is the fill without the bar, whatever is decided.
2. **The test added for review should-fix 3 cannot fail on a squeezed state.** ChainsTreeTitleTests.cs:48-58. It
   passes both M8 and M9 (see above). Compare the state's layout slot with the width its text needs unconstrained,
   for example `LayoutInformation.GetLayoutSlot(state).Width - state.Margin.Left - state.Margin.Right` against a fresh
   `TextBlock` with the same text and font measured at `double.PositiveInfinity`, or assert that
   `LayoutInformation.GetLayoutClip(state)` is null. Then re-run M8 and M9. Otherwise remove it, since its message
   claims what it does not test. Rule 4 itself stays guarded by the older test.

### Consider

1. ChainsTreeAccessibilityTests.cs:92: only the light colours are loaded. The probe shows dark works today; a second
   case with `Fluent.Dark.xaml` would keep it so. If should fix 1 is taken, assert the brush is the accent brush (or
   the bar visible), not merely non-transparent, so a faint fill cannot pass.
2. The high-contrast dictionary's value for the fill is `#F0F0F0` on white. Whether Windows high contrast maps it to
   the system highlight colour at run time was not checked; the live check can include it if the owner uses it.
3. A mouse click on a row's title does not, by reading, select the row (the button handles the mouse-down before
   the tree item sees it); a click on the state text or the row's padding does. That is harmless, but it means a
   highlight appears on some clicks and not others. Listed in the live check so it is not mistaken for a fault.

## Live-check steps for the owner (checked against the code; not run)

These replace the keyboard part of step 2 in `evidence.md`; steps 1 and 3 there are unchanged.

1. **The arrow keys, light theme.** Open the Chains panel with at least two rows. Tab until keyboard focus is in the
   tree (it may land on a row's title button first, shown by that button's focus ring), then press Down. Expected,
   from the code as it stands: the row moved to gets a **faint grey rounded band** behind its header line only (the
   chevron, title and state; not its children's rows), and the previous row loses it. A dashed focus rectangle may
   also surround the row (evidence.md saw one). There is **no** blue accent bar. Judge whether the band is easy to see
   at a glance; this verifier expects it to be hard to see (should fix 1). Left and Right still collapse and expand.
2. **The same in dark theme** (Settings, Dark, which switches without a restart). Expected: the band is a faint
   lighter grey on the dark panel, slightly more visible than in light.
3. **Leaving the tree.** Click elsewhere on the board. Expected: the band stays on the last selected row (the trigger
   is on selection, not focus). It disappears when the tree is rebuilt (any card in a chain changes title, state or
   link).
4. **Mouse.** Click a row's title: the card's details open and the band does not move to that row (expected, see
   consider 3). Click a row's state text ("waiting" or "Ready"): the band moves to that row.
5. **Screen reader** (Narrator, if the owner uses it). Move to a title button. Expected: Narrator reads the full
   title, then "Open this card's details" as the help text.

## Notes on the verifier's own conduct

Only this file was written in the worktree. Everything else (the mutation script, its outputs, the probe test and a
saved copy of the fixed `MainWindow.xaml`) is inside the copy folder. Nothing was deleted, committed or pushed.

## Verdict

**PASS**, with two should-fixes. The suite is green with no warnings, the reproduction test is byte-identical to
518eb05, and all eight requested mutants are caught. Review should-fix 2 is fixed. Review should-fix 1 is met in the
letter (a highlight exists in light and dark, and its trigger is tested) but the highlight is close to invisible
(should fix 1). Review should-fix 3 is not met by the test written for it, which cannot fail (should fix 2); rule 4
remains guarded by the older test.

## Disposition (implementer, overnight, 2026-11-19)

| Finding | Disposition |
|---|---|
| Should fix 1: the highlight is almost invisible | **Fixed as Fluent does it:** a 3 x 16 px accent bar (`AccentFillColorDefaultBrush`) at the left of the selected row, beside the subtle fill. Test `TheSelectedRow_CarriesTheAccentSelectionBar` (absent before selection, present after, 3 px) |
| Should fix 2: the full-width test could not fail | **Corrected (P12: it could not catch what it claimed):** it now compares the width the state's text needs alone with the room it was laid out in and the size it was drawn at. Checked by hand against the MaxWidth="10" mutant: "needs 35.3, has room 10", fails; the line was restored |
| Considers | Recorded: dark-theme tests; the clicking-the-title-does-not-select behaviour; high contrast is for the live look |
