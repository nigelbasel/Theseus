---
name: new-workstream
description: Start a workstream in an agentic factory project - its row under an objective, its branch off the primary branch and a worktree per repo, its records folder and the prompt that starts its session.
argument-hint: "[slug] [project home folder]"
disable-model-invocation: true
---

# Start a workstream

A workstream serves exactly one objective of a project and runs one pipeline: `feature`, `bug` or `freeform`
(`${CLAUDE_PLUGIN_ROOT}/method/pipelines/`). Its **slug** names its records folder, `work/<slug>/`, and its branch,
`<user>/<slug>`, in every repo it changes.

**With Theseus** in a project, a session does this with `propose_workstream`, naming the objective, and the board makes
the card. Use this command when there is no board.

## Ask first

Read `<home>/project.md` (the home folder is in `$ARGUMENTS`, or ask). Then ask, one question each, for what is missing:

1. Which **objective** it serves, from the file's `### Objective <n>.<m>:` headings. If none fits, offer to add one at
   the end of a phase, with its criteria and a `**Pipeline:**` line, in the shape `method/project.md` gives.
2. The **pipeline**: `feature` for new behaviour, `bug` for a defect, `freeform` for anything without gates.
3. The **slug**: lower-case letters, digits and hyphens, not already in the file.
4. The **repos** it changes, from the project's Repos table.
5. Where its **worktrees** go. Suggest `<worktrees folder>/<slug>/<repo>`, one folder per workstream holding a worktree
   per repo, but use whatever the user says.

## Then do it

1. Add its row to the objective's workstreams table, with the table's own columns:
   `| <name> | <slug> | <user>/<slug> | <repos> |`, plus `<what> |` only when the table has the optional `What` column.
   If the objective's `**Pipeline:**` is `[to be chosen]`, set it. Keep the shape `method/project.md` gives, where the
   table follows the `**Pipeline:**` line directly, as Theseus writes it. Theseus reads these lines, so add no column.
2. In each repo, branch off the project's **primary branch**, not the trunk:
   `git -C <clone> fetch origin`, then
   `git -C <clone> worktree add --no-track -b <user>/<slug> <worktree> origin/<primary>`.
   `--no-track` keeps a plain `git push` from landing on the primary. Push the workstream branch with
   `git -C <worktree> push -u origin <user>/<slug>`, which needs no asking; merging into the trunk is never a
   session's.
   In git, a branch cannot be both `<user>/<project>` and `<user>/<project>/<x>`, so the slug is never nested under
   the primary branch's name.
3. Create `<home>/work/<slug>/` and write `brief.md` there: the objective and its criteria, the pipeline, the repos
   and worktrees, and the standing rules the brief carries (no deletes without the owner, no tracker items created
   without their say-so, the trunk only by pull request, the holdout off-limits).
4. Commit `project.md` and the new folder in the projects repository if it is one, staging only those paths.

Finish by giving the command that starts the workstream's session in its first worktree, with full paths:

```bash
cd "<worktree>" && claude --add-dir "<home>" "Read <home>/work/<slug>/brief.md and start at the first stage of the <pipeline> pipeline."
```
