# links-tree-scrollbar: verify of the Ready list change

Pipeline: `bug` (method kit v0), stage **verify of a follow-on change** (question 3, Nigel chose A): commit 3bd5463,
"the Ready list trims long titles too". Verifier: an independent sub-agent that wrote neither this change nor any
earlier stage. Partial independence: same model family as the implementer, and it read `fix.md` (Spec and the new
"Ready list" section) and `verify-review-fixes.md`, with their conclusions, before checking. No holdout scenario was
run; none is named for this change.

Clock (verifier's own, read from the machine): started 21:02:03, measurements finished 21:14:04, this file written
straight after.

## Results

| Check | Result |
|---|---|
| `dotnet test Theseus.slnx --nologo` in the feature worktree | **1564 passed, 0 failed, 0 skipped** (1 m 49 s), exit 0. Extra console lines: only the three "launch ticket" messages tests print on purpose |
| `dotnet build Theseus.slnx --no-incremental` | 0 warnings, 0 errors |
| What the commit touches | `MainWindow.xaml` (the Ready list's title button only), the new test file `ChainsReadyListTests.cs`, and `fix.md`. No existing test changed |
| Copy (no bin/obj/.git), full test project, unmutated | 1564 of 1564, the baseline for the mutations |
| Worktree after the run | Clean (`git status` empty) apart from this file |

## The Ready list's button against the tree's (by reading MainWindow.xaml)

The Ready list's button (MainWindow.xaml:1569-1581) and the tree's title button (MainWindow.xaml:1685-1696) are now
the same, attribute for attribute: `FontSize="12" Padding="0" HorizontalAlignment="Left"
HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0" Cursor="Hand"`,
`AutomationProperties.HelpText="Open this card's details"`, `Command="{Binding Show}"`; a tooltip of two lines, the
title in SemiBold above "Open this card's details"; content a `TextBlock` bound to `Title` with
`TextTrimming="CharacterEllipsis"`.

Nothing else in the list changed: the diff touches only that button and adds one comment line above it. The item's
`Border` (4 px card-colour stripe, padding 8,6, card background), the `StackPanel`, and the `WaitsOn` line below the
title are as before. The one difference left between the two lists is older than this change and outside it: the
tree has a state on the right of the title and hides an empty `WaitsOn` line; the Ready list has neither (a ready
card has no state to show beside it, since the whole list is "ready").

Trimming works only if the list is held to the panel's width. It is: the panel's `ScrollViewer`
(MainWindow.xaml:1425) sets only `VerticalScrollBarVisibility`, so sideways scrolling stays at WPF's default,
Disabled, and the test lays the list out inside a copy of that scroller with its attributes.

## Can the new tests fail?

Each change was made to `MainWindow.xaml` in the copy only, inside the Ready list's block (the tree's identical
button was left alone), and the copy's full test project (1564 tests) was run against it. The copy's file was
restored and compared byte for byte with the worktree's afterwards.

| # | Change (Ready list only) | Tests that fail | Survives? |
|---|---|---|---|
| R1 | remove `TextTrimming="CharacterEllipsis"` | `ChainsReadyListTests.ALongTitle_EndsInAnEllipsisInsideThePanel` ("Expected: CharacterEllipsis, But was: None") | Caught |
| R2 | remove `AutomationProperties.HelpText` | `ChainsReadyListTests.TheTitleButton_TellsAScreenReaderWhatItDoes` (help text empty) | Caught |
| R3 | restore the old button, `Content="{Binding Title}"` with the string tooltip (the commit reverse-applied) | `ChainsReadyListTests.ALongTitle_EndsInAnEllipsisInsideThePanel` ("But was: None") | Caught |

Under R3 the help-text test passes, as `fix.md` says it would: the old string tooltip is read as help text. It guards
the richer tooltip, not the old one.

**Extra probe:** R1 and R3 are caught by the test's first assertion (the trimming setting), so its second assertion
(the title ends inside the panel) never ran. With that first assertion removed from the copy's test and R1 applied,
the second fails on its own: the title's right edge was at 448.7 against a limit of 318.5. So the width check is a
real second guard, not a passenger. The copy's test file was restored and matches the worktree's.

No mutant survives the suite.

## Text check (P17)

- **Tooltip and help text, "Open this card's details".** Checked against what the button does: `Show` is the node's
  command, built in `ChainsViewModel.Node` as `new RelayCommand(_ => _showCard(card.Key))`, documented on
  `ChainNodeViewModel` as "Opens the card's details". Ready nodes are the same objects as the tree's (`Rebuild` adds
  each ready node to both). It is also the old tooltip's wording, word for word. Correct.
- **The tooltip's first line** is `{Binding Title}`, the full, untrimmed title. Correct for rule 3 of the spec.
- **The comment**, "A long title ends in "…", as in the tree (links-tree-scrollbar question 3, Nigel 2026-11-19)."
  True: the tree's title has the same trimming. It cites the decision, dated, as the repo's AGENTS.md asks. Nigel's
  choice is recorded in `fix.md` as 20:58 on 2026-11-19, which matches.
- **`fix.md`'s new section.** Its claims hold: the trimming test fails without the change (R3), the help-text test
  passed before it and fails when the help text is removed (R2), and the full suite is 1564 of 1564.

## Findings

### Must fix

None.

### Should fix

None.

### Consider

1. The width assertion's limit is the panel's content width (318), not the title's own room: the item's 4 px stripe
   and 8 px padding each side leave the title about 298 px. A change that let the title run over the card's padding
   but stop at the panel's edge would pass. Tightening the limit to the item border's inner edge would catch it.
2. No test reads the tooltip's content (the full title above the action line). That is the same for the tree, where
   it was left as a live check; it stays one here (step below).

## Live-check step for the owner (checked against the code; not run)

Open the Chains panel (it is 340 px wide) with a card listed under **READY** whose title is longer than the panel is
wide. That card is also in the CHAINS tree below, so the two can be compared.

Expected, from the code as it stands:

- In the READY list, the title sits on **one line**, in the card's 12 pt text, and **ends in "…"** just inside the
  card's right padding. It is not cut off at the panel's edge, does not wrap, and there is no sideways scrollbar.
- The card's coloured stripe on the left and its "waits on: …" line below the title look as they did before; the
  "waits on" line still wraps.
- Hovering over the title shows a tooltip of two lines: the **full title in semibold**, then "Open this card's
  details".
- Clicking the title opens the card's details, as before.
- The same card's row in the tree trims the same way, with "Ready" kept on the right.

## Notes on the verifier's own conduct

Only this file was written in the worktree. Everything else (the suite and build logs, the mutation script, the
mutant files, the run logs and the probe) is inside the copy folder; no console output went anywhere else. Nothing
was deleted, committed or pushed.

## Verdict

**PASS.** The suite is green (1564 of 1564) with no warnings; the Ready list's button now matches the tree's title
button exactly, and nothing else in the list changed; each of the three requested mutants is caught by one of the
new tests, and the width check can fail on its own. Two considers, no must-fix or should-fix.
