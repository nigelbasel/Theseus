---
name: verifier
description: Give the user the command that starts an independent verifier for a workstream in a fresh session, with the verifier role the holdout guard reads. The implementer never verifies its own work.
argument-hint: "[slug]"
disable-model-invocation: true
---

# Start an independent verifier

The verify stage is run by someone other than the implementer: a **fresh session**, which has not seen the build and
which may read the holdout scenarios. The holdout guard gives a session that role only from its environment,
`THESEUS_ROLE=verifier`, which the session's own commands cannot set. So this session does not start the verifier
itself: it gives the user the command to run in a **new terminal**.

**With Theseus,** a project's verifier card is made by the board when a scenario names the build (`Checks: <slug>`).
Use this command when there is no board.

## Work out

- The workstream's **slug** (`$ARGUMENTS`, or ask), its project home folder, and its worktree of each repo.
- The **holdout folder**: by convention `<repo>-holdout`, beside the repo's clone. Do not open it or list it: this
  session is not a verifier, and the guard refuses it. Just name it.

## Give the user both forms, filled in with full paths

macOS, Linux, or Git Bash on Windows:

```bash
cd "<worktree>" && THESEUS_ROLE=verifier claude --add-dir "<home>" --add-dir "<holdout>" "You are the independent verifier for <slug>. Run /agentic-factory:verify: check the branch <user>/<slug> against <home>/work/<slug>/spec.md and every holdout scenario in <holdout> that names <slug>. Write <home>/work/<slug>/evidence.md, and your detailed results only in <holdout>/results."
```

For a **bug**, the prompt names `<home>/work/<slug>/reproduction.md` and `fix.md` instead of `spec.md`, and the
scenarios covering the behaviour rather than those a spec names; it also asks that the reproduction test pass unchanged
since it failed.

Windows PowerShell:

```powershell
Set-Location "<worktree>"; $env:THESEUS_ROLE = 'verifier'; claude --add-dir "<home>" --add-dir "<holdout>" "<the same prompt>"; Remove-Item Env:THESEUS_ROLE
```

Tell the user: start it in a new terminal, not in this one; it reports a verdict per scenario, and the details stay in
the holdout's `results` folder. Then this session carries on with what does not depend on the verdict, or writes its
handoff and stops.
