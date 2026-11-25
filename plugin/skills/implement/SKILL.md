---
name: implement
description: The implement stage of the feature pipeline, and the reproduce and fix stages of the bug pipeline. Use to write tests first, then the code, on the workstream's branch, until the build and tests are green in every repo changed.
---

# Implement (a bug's reproduce and fix)

**Pipelines:** feature, stage `implement`; bug, stages `reproduce` and `fix` (`${CLAUDE_PLUGIN_ROOT}/method/pipelines/`).
Role: implementer. Work only on the workstream's branch, `<user>/<slug>`, with one worktree per repo.

## A feature

**Produces:** commits on the branch in each repo changed, and `work/<slug>/implementation.md`.

- Tests first, then the code, then the build and the tests green in every repo changed.
- implementation.md says what was done and lists **every** difference from plan.md, however small. That includes
  anything the plan promised that the code does not do, with the reason.
- New tests go in a new file, never beside a reproduction test.
- An existing test changes only to correct a wrong expectation. The change gets its own line in implementation.md,
  saying what the test asserted and why that was wrong.

## A bug

1. **Reproduce.** Write a test that fails for the reported reason, and **commit it on its own, before any fix**. Write
   `work/<slug>/reproduction.md`: the steps, the expected and actual behaviour, the decision or document that says what
   is expected, and the failing output. Gate: the test fails on the unfixed code, for the reported reason. Never
   skipped. Do not push between this commit and the fix on a branch that CI builds and releases from. If the defect
   sits in code no test can reach, a refactor that changes no behaviour may come first, as its own commit with
   characterisation tests; the reviewer checks it separately.
2. **Fix.** Make the smallest change that makes the test pass without weakening it. `work/<slug>/fix.md` says the cause,
   and why the change addresses the cause rather than the symptom.

**Stop patching when the edges keep coming.** At a third fix pass in the same place, record what is known as limits
and propose a feature with a spec instead.

**Next:** the implementer never verifies its own work. Start an independent verifier with `/agentic-factory:verifier`.

With Theseus, call `now_at("Build", ...)` on entering the stage (a bug: `"Reproduce"`, then `"Fix"`).
