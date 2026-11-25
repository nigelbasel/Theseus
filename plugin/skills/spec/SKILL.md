---
name: spec
description: The spec stage of the feature pipeline, and the rule stage of the bug pipeline. Use after research to write what the change must do as decidable criteria, then stop for the owner's acceptance.
---

# Spec (a bug's rule)

**Pipelines:** feature, stage `spec`; bug, stage `rule` (`${CLAUDE_PLUGIN_ROOT}/method/pipelines/`). Role: specifier.

## A feature's spec

**Produces:** `work/<slug>/spec.md`:

- what the change must do, from the user's side, as acceptance criteria a test or a scenario can decide;
- what is out of scope;
- which holdout scenarios apply, **by name only**. Never read or write the holdout folder: it is the independent check.

## A bug's rule

Only when the fix needs a rule the code does not already state (a parser, a policy, a threshold). **Produces:** a
"Spec" section in `work/<slug>/fix.md`: the rule in a few numbered lines, what the code protects against, and what it
does when it cannot be sure. When the expected behaviour is already stated, write no rule and go on to the fix.

## The gate is the owner's, and the session stops here

The spec (a bug's rule) is one of the owner's gates. Once it is written, **say so and stop**. Do not plan until the
version on disk is accepted.

- **With Theseus:** call `now_at("Spec", ...)` on entering. Before planning, call `gates()`. If the owner says "accept"
  in this session, report it with `accept_spec(slug, version, owner_words)`, quoting them; Theseus asks them to confirm
  on the board.
- **Without Theseus:** the owner accepts by saying so in the session. Before you plan, record it as a dated row in the
  project file's Decisions table, quoting their words and naming the spec. A changed spec needs accepting again.

A feature's spec is never skipped.
