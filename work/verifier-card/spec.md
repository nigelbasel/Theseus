# verifier-card: spec

Pipeline: `feature` (method kit v0), stage **spec**, autonomy L0. Branch `nigel/verifier-card`, repo Theseus. From
`research.md`, with Nigel's five decisions of 2026-11-19 (all option A). **Approved by Nigel, 2026-11-19.**

## What changes, from the owner's side

On a card whose change is ready to verify, the owner chooses **Verify with holdouts…**. A new card appears beside it:
a verifier, linked to the card it checks. When the owner starts it, its session reads which holdout scenarios the
change's spec names, runs each, asks the owner in its own tab for any step only a person can do, writes the details
in the private holdout repo, and reports each verdict back to the card it checked: name, pass or fail, one line.
When the owner confirms that report, the verifier card stops and closes on its own.

No implementing session can read the scenarios, and the verifier can read them but cannot change them or the code.

## Acceptance criteria

**First, the assumption (D1)**

0. Before anything else is built, it is measured on this machine that a PreToolUse hook sees an environment variable
   set on the `claude` process by the process that starts it. If it does not, work stops and returns to Nigel.

**A role, and how a session gets it (D1)**

1. A card has an optional **role**: none, or verifier. It is kept in the board's own record of the card (the sidecar),
   so a session rewriting its plan file cannot drop it.
2. A launch carries environment variables for the `claude` process. A verifier card's session starts with
   `THESEUS_ROLE=verifier`; a card with no role starts exactly as today, with no variable set.
3. The role is not offered anywhere a session can set it: not in a proposal, not through any MCP tool.

**The guard (D2, D3)**

4. A generic holdout guard is in the repo, `tools/holdout/holdout-guard.js`, with tests like the lock guard's. It holds
   no owner path or repo name (P19): it reads the holdout clone's folder, and optionally its remote address, from
   Theseus's settings. With none set, it allows everything.
5. By role, for any tool whose path, command, URL or pattern names the holdout location:
   - **no role:** denied, as today;
   - **verifier:** reading, searching and listing are allowed; writing and editing are allowed only under the holdout
     clone's `results/` folder; shell commands naming the holdout are denied;
   - **author** (`THESEUS_HOLDOUT_AUTHOR=1`, as today): allowed.
6. No shell command runs with its working directory inside the holdout, for any role but author. **Added in fix pass
   2 (M1), approved by Nigel 2026-11-19:** it also binds a session with no role, such as the owner's own session working in
   the holdout without `THESEUS_HOLDOUT_AUTHOR=1`. A verifier may not write or edit any file outside the holdout's `results/` folder. This is a tripwire for accidents,
   like the lock guard: shell commands in the code repos (building, testing) are not policed.
7. The holdout folder and remote address are set in Settings. Verify with holdouts… is unavailable while the folder
   is not set, and says why.
8. The guard is installed to the same place, the same way, as the lock guard. The owner's personal guard is retired
   only with Nigel's OK, after the new one is checked.

**Starting a verifier (D4)**

9. **Verify with holdouts…** is in the card's context menu and in the Workstream menu (R16), for a card that is not
   itself a verifier.
10. It creates a card titled "Verify: <card title>" with role verifier, its working directory the checked card's
    working directory, and the holdout clone and the checked card's other extra directories as its extra directories.
    **Changed in fix pass 2 (overnight decision 3), approved by Nigel 2026-11-19:** first approved as "working
    directory the holdout clone"; swapped so the guard can refuse every shell command run inside the holdout (verify
    part 1, M1). It adds a hands-over link
    from the checked card and a reports-back link to it, whose document is
    `results/<checked card key>-<yyyyMMdd-HHmm>.md` in the holdout clone.
11. Its prompt is a fixed brief:
    - find the checked change's spec under `work/` in the checked card's repos, and its section naming the holdout
      scenarios that apply;
    - read the scenarios with the file tools, and run builds and tests from the checked card's folder, never from
      inside the holdout (**added in fix pass 2 with AC 10's change, approved by Nigel 2026-11-19**);
    - run each named scenario;
    - ask the owner in this tab for any step only a person can do, quoting the scenario's step;
    - write the details, steps and observations to the report document;
    - report back with a summary that gives only, for each scenario, its name, pass or fail, and one line of reason;
    - change nothing in the checked card's repos.
12. Nothing starts until the owner presses Start, as for every card.

**Ending (D5)**

13. When the owner **confirms** a report on a verifier card's reports-back link, Theseus starts that card's stop
    exactly as a Stop press does, and logs that it did. Marking a link satisfied by hand, or leaving it, stops nothing.
    No other card ever stops without a press.

## Out of scope

- Reading which stage a card is at (objective 1.1); the owner decides when to verify.
- Running scenarios on CI (objective 1.4).
- Enforcing what the verifier's summary says: the brief asks for name, result and reason only, and the owner reads it
  before confirming. Recorded as a known limit.
- The remint defect in the research (a failed resume restarted under a new id keeps the old MCP address).

## Holdout scenarios that apply

All three, run through the new verifier card as this feature's live check. Their first real job is the stage 0
changes and close-step, pull request #1, which is held open for `stop-with-handoff`.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| spec | after research (4e675f6), decisions D1 to D5 taken one at a time | this commit's time | |
