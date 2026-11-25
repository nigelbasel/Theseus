---
name: review
description: The review and compound stages. Use, in a session other than the implementer's, to review a workstream's diff against its spec and the repo's reviewer rubric, then to write the lessons for the method.
---

# Review, then compound

**Pipelines:** feature, stages `review` and `compound`; bug, the same two, only when its rule stage ran
(`${CLAUDE_PLUGIN_ROOT}/method/pipelines/`). Role: reviewer, **independent of the implementer**.

## Review

**Inputs:** the diff in each repo, `spec.md` (a bug's `fix.md`), `evidence.md`, and the reviewer rubric of each repo,
which its `AGENTS.md` names. If a repo has no rubric, say so, and review against the spec and the repo's own
conventions.

**Produces:** `work/<slug>/review.md`: findings, each citing the spec line or the rubric item it breaks, with the file
and line. A finding is either fixed in code or explicitly accepted by the owner.

**Gate:** evidence, and blocking: review.md has no open finding, and the compound stage has been reached.

## Compound

**Produces:** `work/<slug>/lessons.md`: what the method caught, what it missed, and what to change in a pipeline, the
rubric or `AGENTS.md` so it is caught next time. Changes to the method are **proposals only**, in a separate commit;
the owner decides.

With Theseus, call `now_at("Review", ...)`, then `now_at("Lessons", ...)`, on entering each.
