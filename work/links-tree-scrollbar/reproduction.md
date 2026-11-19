# links-tree-scrollbar: reproduction

Pipeline: `bug` (method kit v0), stage **reproduce**, autonomy L0. Branch `nigel/links-tree-scrollbar`, repo Theseus.

## Report

Nigel, 2026-11-19: "the horizontal scrollbar overwrites the bottom of the tree" in the Chains panel.

## Expected and actual

- **Expected:** every row of the Chains tree can be read; nothing is drawn over a row.
- **Actual:** when a card's title is wider than the panel, a horizontal scrollbar appears and is drawn over the tree's
  bottom row.

## Cause

The tree is a `TreeView` (`src/Theseus.App/MainWindow.xaml:1584`) inside the panel's own vertical `ScrollViewer`
(`:1425`). A `TreeView` has its own scroll viewer, which shows a horizontal bar when a row is wider than the panel
(a row's title is a button that is never trimmed). Under WPF's Fluent theme, which the board runs under
(`ThemeApplier` sets `ThemeMode`), a scroll viewer lays its bars **over** its content instead of beside it, so the bar
covers the bottom row. Under the classic theme it would take a row of its own.

Two earlier versions of the test passed on the unfixed code, and were not a reproduction:
1. Without the Fluent theme the bar gets its own row, and nothing is covered.
2. With the theme, asserting viewport against extent cannot see an overlay, and `IsVisible` is always false for an
   element that is not in a window (P16: a test that could not fail).

## The failing test

`tests/Theseus.Tests/Repository/ChainsTreeLayoutTests.cs`, `ALongTitle_DoesNotCoverTheTreesBottomRow`. It lays out
the tree's own markup from `MainWindow.xaml`, inside a scroll viewer with the panel's attributes, under the Fluent
theme, at the panel's content width (318), and checks that no visible horizontal scrollbar is drawn over the last row.

Its output on the unfixed code:

```
tree 318x65.9; viewport 318x65.9; extent 510.2x65.9; horizontal bar Visible; style ScrollViewer; last row ends at 65.9
  bar Horizontal Visible 318x12 at y 53.9
Failed ALongTitle_DoesNotCoverTheTreesBottomRow
  a horizontal scrollbar is drawn over the bottom row (row ends at 65.9)
```

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| reproduce | logged at the stage start in the workstream log | this commit's time | Three test versions; the third fails for the reported reason |
