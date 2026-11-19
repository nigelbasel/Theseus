# Bootstrap stage 0: retrospective

Stage 0 of the Theseus bootstrap (`projects/theseus-bootstrap.md`): seed method kit v0, then take real changes through
it by hand, at autonomy L0. Run 18 to 19 November 2026 in one workstream. **A draft for Nigel's review against the exit
criteria below**; nothing here is accepted until he says so.

Every figure comes from a record: the stage tables in `work/<slug>/`, the workstream log, git, or a sub-agent's own
report. Where one is missing it says so.

## Exit criteria

| # | Criterion | Evidence | State |
|---|---|---|---|
| 1 | AGENTS.md exists, CLAUDE.md points to it, nothing lost | `AGENTS.md`, `CLAUDE.md` (c85f1ac); there was no earlier CLAUDE.md | **Met** |
| 2 | Reviewer rubric exists, every item traced to a dated decision | `.agents/reviewer-rubric.md` (moved out of `method/` on 2026-11-19, as Theseus's own), 19 items, each citing file and date; used by four reviews, none found an untraced item | **Met** |
| 3 | `freeform`, `feature` and `bug` defined; `freeform` matches ad hoc with no added steps | `method/pipelines/`; `freeform` is one stage, no gates | **Met** in writing. Ad hoc work was not run through it in stage 0 (below) |
| 4 | Pipelines and artefact paths work for a workstream spanning several repos | Artefacts and evidence are defined per workstream with one result per repo (`method/project.md`); card-branches made the board show several repos | **Partly met.** The format allows it, but no change in stage 0 touched more than one repo, so it is untested in use |
| 5 | A plain-file project format, with the bootstrap as the first example, stages 0 to 5 as phases | `method/project.md`, `projects/theseus-bootstrap.md` | **Met** |
| 6 | Three holdout scenarios written, stored outside agent-writable paths, with pass/fail | Private repo `nigelbasel/Theseus-holdout` (resume tomorrow, stop with handoff, lock contention), each with pass and fail criteria; a user-level hook refuses access unless a session is started as the author | **Met** as written. **None was run** (below) |
| 7 | Five Theseus changes through the pipeline, artefacts in the repo | Six: readme-runtime, guard-mentions, stop-timing, resumed-stop, card-branches, parked-cores, each with `work/<slug>/` | **Met** |
| 8 | This file written, and reviewed by Nigel against these criteria | This file | **Waiting for Nigel** |

## The six changes

| Change | Pipeline | Fix passes | Verifies (failed) | Sub-agent tokens | Notes |
|---|---|---|---|---|---|
| readme-runtime | bug | 1 | 1 (0) | 126,911 [from the session; not written into its record] | README asked for the wrong .NET runtime |
| guard-mentions | bug | 3 | 3 (2) | 444,347 | Lock guard: a mention refused or excused a run. Third verify stopped by a safety classifier part way |
| stop-timing | feature | 2 | 3 (0) | 439,183 | Logs how long each stop took, or how far it got. Live check found resumed-stop |
| resumed-stop | bug, with review and compound added | 2, plus review fixes | 3 (0) | 593,750 | A resumed session was never closed by a second stop |
| card-branches | feature | 2, plus review fixes | 2 (1) | 471,480 | A card's repos and their branches; PR links not guessed |
| parked-cores | feature | 3, plus a test tidy | 3 (1) | 583,388 | Status-bar warning when cores are held back |
| **Total** | | | **15 (4)** | **2,659,059** | |

The implementer's own tokens were not measured in stage 0. Its own working time per feature was about 25 to 30
minutes; elapsed time per feature was hours, mostly gates, CI releases, board updates and Nigel's live checks.

## What the method caught

- **Independent verification found something real in every non-trivial change.** Four verifies failed outright; others
  passed with gaps that mattered. Examples: a regression the first fix introduced (guard-mentions, parked-cores); a
  tooltip WPF never displayed (card-branches); a tooltip naming the wrong Windows setting (parked-cores); one record
  used for two meanings (resumed-stop).
- **Live checks found what no test could.** stop-timing's live check found that a resumed session was never closed by a
  second stop, which became resumed-stop; it also measured, for the first time, how long closes really take.
- **Research changed what was built,** three times out of four features: stop-timing (half the stops never finished),
  card-branches (cards did not know their repos), parked-cores (the obvious rule would miss most clamps).
- **The rule stage and the spec gate stopped circling.** guard-mentions took three passes until its rule was written
  down; the bug pipeline gained a rule stage from it, and resumed-stop used it.
- **Review found what verifiers missed,** in every change it ran on: a fallback to a guessed link, a stale tooltip, a
  record that meant two things.
- **The method revised itself 17 times** (P1 to P17 in `method/`), every one from something a change taught.

## What it missed, or got wrong

- **The holdout scenarios were never run.** They were written first, protected, named in specs, and then not exercised:
  no session was started as the author, and live checks were done ad hoc instead. Stage 1 must run them.
- **Independence was partial.** Every verifier and reviewer was a sub-agent briefed by the implementer. They found a lot,
  but their brief set what they looked at.
- **The implementer's own errors were frequent and recurring:** estimated times written into records (twice), tests that
  asserted the implementer's own wrong words (twice), a test that could not fail (several), instructions to Nigel written
  from memory rather than the code (twice), a tests-first slip, and claims of cause made before they were checked.
- **The implementer damaged a file outside the work:** Nigel's memory index, double-encoded by a PowerShell rewrite.
  Repaired exactly; it became rule P14.
- **A sub-agent deleted its own temp files without asking,** because the brief did not carry Nigel's rule. It became P2.
- **Multi-repo work was never actually done** (criterion 4).

## Friction

- **Ad hoc work** was not run through `freeform` in stage 0, so its "no extra clicks" promise is untested in use. The
  method itself adds nothing to it by design.
- **Project work at L0 is gate-heavy.** Every stage waited on Nigel's word: research choice, spec, plan, each verify,
  review and lessons. A feature took six to eight of his decisions. That is the point of L0, and the reason L1 and L2
  exist.
- **Live checks cost a release cycle** (push, CI, update the board, restart) and Nigel's time at the board.
- **Tooling got in the way:** a safety classifier stopped a verifier's adversarial testing; a push was refused because
  one "yes" answered two questions; Windows PowerShell 5.1's encoding; a newly approved card landing in a folded view.

## Findings about Theseus itself (for its backlog, Nigel's decision)

| Finding | From |
|---|---|
| Closes wait for an unrelated refresh: 87 s, 112 s, and once 11 minutes, against a 20 s quiet period | stop-timing, resumed-stop (L1) |
| A card shows no sign that a stop is under way, inviting a second press | stop-timing (L2) |
| The close step's idea of which run a stop belongs to; and the parked-cores "blink" | resumed-stop V1, V2; parked-cores review 4 |
| Proposed together as a **close-step** feature | `projects/theseus-bootstrap.md` |
| A newly approved card can land in a folded view column, unseen | stop-timing live check |
| The lock guard's eight older bypasses (a cooperating session would not write them) | guard-mentions |
| Estimating effort from recorded times (Nigel's idea, 2026-11-19), a candidate after stage 1 | parked-cores lessons |

## Decisions made in stage 0 (to take back to the planning chat)

| Date | Decision |
|---|---|
| 2026-11-18 | Kit in `method/`; artefacts in `work/`; retrospectives in `bootstrap/`; holdouts in a private repo behind a hook |
| 2026-11-18 | Short slugs for changes, no GitHub Issues for now |
| 2026-11-18 | A bug that needs a rule gets a rule stage, and then review and compound (P5, P7) |
| 2026-11-18 | The lock guard protects against accidents by cooperating sessions, not against evasion |
| 2026-11-19 | Stop patching at the third pass that still finds edges; propose a feature instead (P10) |
| 2026-11-19 | Terminology in conversation: Agentic Factory Projects and Agentic Factory Workstreams; ad hoc work stays a plain workstream. Words only |

## How the method is organised (worked out with Nigel, 2026-11-19, after the changes)

These questions came up at the end of stage 0. They are proposals for the planning chat, not decisions.

**Three layers, each with its own home.**

| Layer | Holds | Where it lives | Changes |
|---|---|---|---|
| **The kit** (common) | Pipelines, the project format, the general rules | One shared, versioned place for everyone using the method: its own repo or a plugin | Rarely; improved by approved lessons |
| **Per repo** | The repo's map (`AGENTS.md`) and its own rubric, drawn from that codebase's dated decisions | In that repo | Once per codebase, then small additions |
| **Per project** | Objective, phases, exit gates, which pipelines and **which kit version**, autonomy per repo, the work artefacts | The project's home repo | Every project: this is the part that is genuinely the owner's |

On 2026-11-19 Theseus's own rubric was moved out of `method/` to `.agents/reviewer-rubric.md` (Nigel), so `method/` now
holds only the common kit and can later be split into its own repo with its history (for example a public
`theseus-method` repo). The split itself waits until a second codebase is about to use the method. Lessons go to the layer they belong to: general rules to the kit,
codebase lessons to that repo's rubric or AGENTS.md, project lessons to the project file. A project pins a kit version,
so improving the kit never changes a project already running underneath it.

**Starting a project needs no method work.** The owner states the objective; an agent drafts the project file from the
kit's template and, for a repo that lacks them, its AGENTS.md and rubric; the owner approves once; workstreams then run
the standard pipelines and the owner meets their gates. The owner selects, and does not create.

**How stable it is, on stage 0's evidence (one codebase):** six changes used the same two pipelines; all 17 lessons were
general rules, none specific to Theseus's code; the rubric was written once and used unchanged by four reviews. The
expectation is that the kit stays stable and the per-repo layer carries the differences between codebases. The team
pilot (phase 5) is where that is tested.

**A new method, from small to large:**

| Size | When | What it takes |
|---|---|---|
| A new pipeline in the existing kit (the common case) | A different kind of work: incidents, migrations, research, documentation, legacy ports | One pipeline file; two or three real items through it at L0; compound refines it |
| A fork of the kit | A different team or context with different ground rules | Copy, change what differs, version separately; projects choose their kit |
| From scratch | A different way of working, where the kit's assumptions do not hold | The stage 0 recipe: seed kit v0 from the conventions already practised (an agent drafts it in minutes); shake it down on about five real changes at L0, which is where the cost is (stage 0: six changes, about a day and a half); let compound grow it; raise autonomy only on evidence |

A method is trusted only once it has carried real work: a drafted method is a hypothesis, and the shakedown is the test.

## For stage 1

- Run the three holdout scenarios, by an author session or by Nigel, and automate them on Windows CI.
- Do one change that spans two repos.
- Measure the implementer's own tokens, and Nigel's time at gates.
- Decide whether verifier and reviewer independence needs more than a fresh sub-agent.
- Decide where the kit lives (its own repo or a plugin) and have the project file name the kit and version it runs,
  before a second codebase uses the method. (Theseus's rubric is already out of `method/`.)
