# Plan: objective 1.1, pull request 1 (the format and reading projects)

Pipeline `feature`, stage **plan**, for `spec.md` ACs 1 to 11 and the `method-v0.2` release. Pull requests 2 (the
project card and view) and 3 (the terminal window) are not in this plan.

## Order

1. **method-v0.2** (no code). `method/project.md` tightened to the shape of AC 3; `feature.yaml` and `bug.yaml` mark
   plan and review as evidence gates inside a project (AC 7, departure 5); the `theseus-evidence` result block
   documented in the method (from the evidence-block proposal, with its answered questions); P18 is already in the
   README's rules, and the README's version moves to `method-v0.2`. `projects/theseus-bootstrap.md` rewritten to the
   new shape with `Kit: method-v0.2` and `Primary branch: nigel/theseus`, every criterion's text and tick kept.
   *Verified by:* the reader's test over the real project file (step 2), and `PublicFilesTests`.
2. **The project file reader** (AC 3, 11), `src/Theseus.Core/Projects/`: `ProjectFileReader` returning a
   `ProjectFileReading` (readable, pinned to method-v0.1, or unreadable with line and reason) over `ProjectFile`,
   `ProjectPhase`, `ProjectObjective`, `ProjectCriterion`, `ProjectWorkstreamRow`, `PipelineKind`.
   *Tests first:* `tests/Theseus.Tests/Projects/ProjectFileReaderTests.cs`, a test per shape and per refusal, and one
   that reads the repository's own `projects/theseus-bootstrap.md`.
3. **Reading with git** (AC 2, 5): `IGitReader` and `GitCli` (runs `git -C <folder> ...`, read-only commands only,
   never fetch); `ProjectCatalog` finds the primary branch from the default branch's copy of each project file and
   reads the file from the primary; `WorkstreamArtefactReader` chooses the source branch for a slug (its own branch
   while unmerged, else the primary, else not on this machine) and reads its `work/<slug>/` artefacts.
   *Tests first:* against real throwaway repositories made in the temp folder (git is on every machine that builds
   Theseus); commits there pass identity with `-c` so no git config is touched.
4. **Stages and gates** (AC 6 to 9): `StageCalculator` over `WorkstreamArtefacts` (pure), `EvidenceBlockReader` for
   the last `theseus-evidence` block, `SpecAcceptanceStore` keyed by project, slug and the accepted spec's commit.
   *Tests first:* a test per stage artefact, per gate state, the ahead-of-gate case, and work from before v0.2.
5. **Membership and placement** (AC 4, 10): `Workstream.Project` and `Workstream.Slug`, kept in the board file;
   `ProjectMembership` (slug clash: two cards share a slug only when one is a verifier); `ProjectLayout` (cards at
   their stage per objective, No slug yet, Not in the project file, not started, earlier slugs, the verifier in
   verify). `propose_workstream` gains `slug`; an approved proposal from a project card inherits its project; a
   verifier card takes its subject's project and slug.
   *Tests first:* persistence across a refresh and another write (P21), the layout cases, the MCP tool's schema and
   refusal of a malformed slug.
6. **App** (AC 1, 4): Settings gains **Project repositories** (one folder per line); the card menu and the menu bar's
   Workstream menu gain **Project** (project, then slug, or clear), shown only while a project is readable.
   *Tests first:* the setting saves and reads back; the board with no repositories set offers no item (AC 22).

## Risks

- `git` runs as a child process on refresh. With no repositories set nothing runs (AC 22). A slow repository logs
  itself through the refresh's existing step timing.
- Real-git tests depend on git on the path. CI's `windows-latest` has it.
