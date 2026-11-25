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
- **Without Theseus** there is no record a session cannot write, so this gate is weaker and rests on discipline. Only
  the owner's explicit acceptance of this spec counts, such as "accept spec.md": approval of something else, or
  "looks fine", is not acceptance. Best, the owner writes the Decisions row themselves. Otherwise, write it only after
  such words, quoting them exactly, naming the spec and dated, and say in the row that the session wrote it. A changed
  spec needs accepting again.

A feature's spec is never skipped.
