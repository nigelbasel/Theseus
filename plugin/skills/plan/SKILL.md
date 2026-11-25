---
name: plan
description: The plan stage of the feature pipeline. Use once the owner has accepted spec.md, to write the steps, the files, the tests to write first and the risks.
---

# Plan

**Pipeline:** feature, stage `plan` (`${CLAUDE_PLUGIN_ROOT}/method/pipelines/feature.yaml`). Role: planner.

**Before you start,** check that the spec on disk is accepted. With Theseus, `gates()` says so. Without Theseus, the
project file's Decisions table has the owner's acceptance of this version. If it is not accepted, stop: see
`/agentic-factory:spec`.

**Produces:** `work/<slug>/plan.md`:

- the steps, the files in each repo, and the tests to write first;
- for each step, how it will be verified;
- the risks;
- when code moves out of an existing sequence (a refresh, a pipeline, a lock), what that sequence guaranteed (fresh
  reads, order, locks held) and how the new path keeps each;
- any criterion a live check cannot reach, named, so review knows where to look.

**Gate:** evidence, inside a project. It passes when plan.md exists and the implement stage has been reached, and it
does not block. Go on to `/agentic-factory:implement`.

With Theseus, call `now_at("Plan", ...)` on entering the stage.
