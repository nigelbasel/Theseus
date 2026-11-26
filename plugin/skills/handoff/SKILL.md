---
name: handoff
description: Write or rewrite this workstream's handoff, the only thing the next session is given - what was done, what is unfinished and what to do next. Use when asked to stop, wrap up, hand over or leave things for the next session.
argument-hint: "[slug or handoff file]"
---

# Write the handoff

The handoff is the **only** thing the next session is given: anything left out of it is lost. It is one file per
workstream, rewritten in place each time, never a new dated file.

## Where it goes

1. If the session's prompt or brief names a handoff file, that one. A Theseus card's prompt always does.
2. In a project: `<home>/work/<slug>/handoff.md`.
3. Otherwise ask the user where, once, and say the path in the handoff itself.

## What it says

```markdown
# Handoff: <workstream>

Updated <yyyy-mm-dd HH:mm>, by <session or person>.

## Code
| Repo | Worktree | Branch | Pull request |
|---|---|---|---|

## Where it is
The pipeline and the stage reached, and the records written so far under work/<slug>/.

## Done
What was done this session, each with its evidence (a test run's figures, a commit, a file).

## Unfinished
What is started and not done, and why.

## Waiting on the owner
Each parked question, where it is written, and what does not depend on it.

## Next
The first thing the next session should do, as an instruction it can follow without asking.
```

Use full paths and full URLs. Figures are measured or written `[not recorded]`, never estimated. Commit the handoff
in the projects repository if it is one, staging only its own path.
