---
name: research
description: The research stage of the agentic factory's feature pipeline. Use when a workstream starts on a feature and has to find out what exists, what the change touches and the options, before a spec.
---

# Research

**Pipeline:** feature (`${CLAUDE_PLUGIN_ROOT}/method/pipelines/feature.yaml`, stage `research`). Role: researcher.

**Produces:** `work/<slug>/research.md` in the project's home folder:

- what exists today, in each repo the change touches;
- prior decisions that constrain it (the project file's Decisions table, the repo's `AGENTS.md` and its decision
  records);
- options, each with its trade-offs, and the one you recommend;
- when the feature reads an external system (an API, counters, a file format), its failure modes as well as its happy
  path: what errors come back, how often, and whether they pass. Measure them; never assume.

**No code changes** in this stage.

**Gate:** none. Go on to `/agentic-factory:spec` when research.md is written.

**May be skipped** when an approved project spec, or another workstream's research, already covers it. Say so: with
Theseus, `now_at("Research", "skipped: <reason>")`; without, a line `Research: <reason>` in `work/<slug>/skipped.md`.

With Theseus, call `now_at("Research", "<what you are doing>")` on entering the stage.

Never invent a figure: write a bracketed placeholder, such as `[not measured]`, and list it.
