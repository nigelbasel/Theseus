---
name: verify
description: The verify stage, for an independent verifier session only. Use when this session was started as a verifier (THESEUS_ROLE=verifier) to check another session's branch against its spec and the holdout scenarios.
---

# Verify

**Pipelines:** feature and bug, stage `verify` (`${CLAUDE_PLUGIN_ROOT}/method/pipelines/`). Role: verifier,
**independent of the implementer**. If this session wrote the code, stop, and start a verifier with
`/agentic-factory:verifier` instead.

**Produces:** `work/<slug>/evidence.md` in the project's home folder. It is prose, ending in a `theseus-evidence`
block, whose shape is in `${CLAUDE_PLUGIN_ROOT}/method/project.md`. It gives one result per repo changed:

- the build and the **full** test suite, as measured figures: passed, total, warnings;
- for a bug, the reproduction test passing, **unchanged** since it failed (a plain diff check);
- each holdout scenario the spec names, run, as pass or fail with its evidence;
- each guard or condition the change adds, removed in a copy: report any test that still passes without it;
- for UI behaviour, "unchanged" checked against what is rendered, not against the source;
- steps a person is asked to carry out, checked against the code first, each saying what it should produce.

**Holdouts:** a verifier may read the holdout scenarios with the file tools, and write only in the holdout's `results`
folder. It never runs a shell command in the holdout. The builder learns only the verdict: each scenario's name, pass
or fail, one line each.

**Gate:** evidence, and blocking. `result` is `pass` only when every repo passes and every named scenario was run by an
independent verifier and passed. A named scenario nobody could run keeps the stage **open**, so the gap shows. Verify
is never skipped.

With Theseus, call `now_at("Verify", ...)` on entering, and `report_back` with the verdict when done. Without Theseus,
write the verdict at the end of evidence.md and tell the owner.
