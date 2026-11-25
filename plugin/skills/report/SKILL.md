---
name: report
description: Write the workstream's report, REPORT.md, when its work is done or can go no further without the owner, including everything decided without the owner. Then stop.
argument-hint: "[slug or report file]"
---

# Write the report

A report is the workstream's result for whoever it reports to. It is a **claim until the owner confirms it**.

## Where it goes

1. If the prompt or brief names a report file, that one.
2. In a project: `<home>/work/<slug>/REPORT.md`.
3. Otherwise ask the user where, once.

## What it says

```markdown
# Report: <workstream>

**Verdict:** done | partial | go | needs-clarification | kill

## Summary
Three to five lines: what was asked, what was done, what is left.

## Criteria
| Criterion | Met? | Evidence |
|---|---|---|

## What was done
Each piece, with its evidence: commits, test figures, files, pull request URLs.

## Decided without the owner
| Decision | Option passed over | Why | Reversible by |
|---|---|---|---|

## Waiting on the owner
Each parked question, its options and the recommendation.

## Not done, and why
```

Every choice taken on the "decide and record" rung of the ladder is listed under **Decided without the owner**, with
the option passed over, so the owner can overturn it at review. Verdict `partial` when anything waits on the owner.

## Then

- **With Theseus:** call `report_back` with the report's path, a short summary and the verdict, and tell the user it
  waits for their confirmation on the board.
- **Without Theseus:** tell the user the report's full path and that it is ready for them.

Write the handoff too (`/agentic-factory:handoff`), then stop. A finished session does not wait in its slot.
