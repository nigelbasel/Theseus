# links-tree-scrollbar: lessons

Pipeline: `bug` with a rule stage (method kit v0), stage **compound**. Rule A approved by Nigel; the rest run
overnight under his autonomy brief (2026-11-19). Branch `nigel/links-tree-scrollbar`, not pushed. Proposals only.

## Time and cost (measured)

| Stage | From | To | Notes |
|---|---|---|---|
| reproduce | 18:35 (logged) | 18:42:35 | Three versions of the test; only the third failed for the reported reason |
| rule | 18:55 | 18:56 | Nigel chose A |
| fix | 18:56 | 18:57:57 | The attribute alone was not enough: the row template too |
| verify | 18:58:27 | 19:27:57 | 158,728 tokens. PASS |
| should-fix (template tests) | | 19:32:02 | |
| review | 19:32:20 | 19:35:18 | 145,555 tokens. No must-fix; three should-fix |
| review fixes | | 19:44:34 | |
| re-verify | 19:45:08 | 20:11 | 163,412 tokens. PASS; two should-fix |
| should-fix | | 20:14:50 | 1562 of 1562 |

Sub-agents: 467,695 tokens. Nigel's live look is still to do.

## What the method caught

- **Checking what is rendered (P11) found the real cause.** The bug only exists under the Fluent theme, whose scroll
  bars lie over the content; a test without the theme passed on the broken code.
- **Tests on the layout found that the first fix was not enough:** with sideways scrolling off, the rows were still
  laid out wider than the tree, because the standard tree item never holds its header to the tree's width.
- **The review found accessibility the fix had lost:** keyboard selection showed nothing, and the screen-reader help
  text was gone with the string tooltip.
- **The re-verify measured the contrast** of the highlight against the panel (about 1.1:1) and found Fluent's accent
  bar was missing; and found that a test written for the review could not fail.
- **Mutation testing** held every part of the rule and the template, round by round.

## What it missed, or got wrong

- **Two test versions could not fail** before the reproduction worked: one without the theme, one asserting a
  measure an overlay does not change, plus `IsVisible`, which is always false outside a window.
- **A test host loaded Fluent's styles but not its colours**, so a working trigger looked broken.
- **A test written to answer a review finding could not fail**, and was only caught by the re-verify.
- **A brush was chosen by name** ("Windows 11's list-selection brush") without checking its contrast.

## Proposed changes to the method kit

| # | Where | Change |
|---|---|---|
| P26 | `method/README.md`, rules (UI) | **A UI test renders under the app's real theme: its styles and its colours**, and checks both light and dark where colour matters; and a highlight or mark is checked for contrast against what it sits on |
| P27 | `method/README.md`, rules | **A test written to answer a review or verify finding is mutated by its author before it is committed**: the mutant the finding describes must fail it. Done by hand tonight for the full-width test; it should be the rule |

## Follow-ups (Nigel's decision)

- The live look (evidence and verify-review-fixes list the steps): a long title, expand and collapse, the tooltip,
  and the arrow-key highlight with its accent bar in light and dark.
- Question 3: the panel's "Ready" list has the same untrimmed title.
- The three tree test files share about 60 lines of helper; worth one helper when a fourth arrives.

## Record

| Stage | Started | Finished |
|---|---|---|
| compound | 20:15 | this commit's time |
