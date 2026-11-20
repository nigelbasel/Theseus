# Spec: objective 1.1, the project view

**Status:** accepted by Nigel at the spec gate, 2026-11-20 (draft 3). Draft 2 was written against his five decisions of that
morning and reviewed the same day (4 must-change, 12 should-change, all addressed here; the review is in this
session's records and summarised at the end). Drafts 1 and 2 are in this file's git history. Pipeline `feature`,
project `theseus-bootstrap`, phase 1. Items marked **[default]** were not decided by Nigel; each is the reversible
choice and can be changed at the gate.

**Delivered as three pull requests into `nigel/theseus`, in order:** (1) the project file format and `method-v0.2`
(with the evidence result block and P18, which the approved first 1.1 criterion releases together), reading
projects, membership and gates; (2) the project card and the project view; (3) the project's terminal window.

## Departures from the approved criteria (P24)

Against the 1.1 criteria approved on 2026-11-19 (already amended in `projects/theseus-bootstrap.md` for decisions
1 to 5), this spec:

1. Replaces **a row per project** (a folding row header that brings the window forward) with **one project card**
   that opens a whole-screen project view (decision 1).
2. Replaces "appears in its project's row at its stage, in view" with "shows in the project card's count and at its
   stage in the view" (decision 1). The separate fix for approved cards landing in a folded column is **not**
   carried; if ad hoc cards still go missing, that is its own bug.
3. Replaces "progress against its exit gate" with **n of m objectives done**, with the exit gate's text shown in the
   view: phase 1's exit gate is three facts, not a count. **[default]**
4. **Draws no links** between cards in this version, which Nigel's proposal of 2026-11-19 included. **[default]**
5. Changes the **pipelines' owner gates** inside a project, following decision 4A ("each objective's spec, and the
   primary's PR into `main`"): plan and review stop being the owner's gates and pass on their artefact; a bug's rule
   stays the owner's, as the bug's spec. The pipeline files change in `method-v0.2` (AC 7).

## What changes, from the owner's side

A project shows on the board as one card: its name, current phase, objectives done, and a count of what is waiting on
him. Opening it fills the main area with the project: a tree of phases, objectives and workstreams on the left; on the
right, each objective's workstreams laid out by pipeline stage, with the gates between stages marked and the specs
waiting on him actionable. The project's sessions open in one terminal window of their own. Ad hoc work is unchanged.

## Acceptance criteria

### The format and reading projects (pull request 1)

1. **Which projects.** Settings gains **Project repositories**: folders, one per line, each a clone or worktree of a
   project's home repo. **[default]** Empty by default, so a board with none set behaves exactly as today (AC 22).
2. **Read from the branch, not the checkout.** Theseus reads `projects/*.md` and `work/<slug>/` with git from the
   project's **primary branch** (`git show <primary>:<path>`) in the listed repository, so it does not matter which
   branch that folder has checked out; a workstream's own branch is read the same way while it is not merged (AC 5).
   The primary branch is found from the project file on the repository's default branch the first time, then from
   the primary itself. A folder that is not a git repository, or has no such branch locally, shows as **unreadable:
   <reason>**. Theseus fetches nothing; it reads what is on the machine.
3. **The project file's shape**, tightened in `method/project.md` for `method-v0.2`, with
   `projects/theseus-bootstrap.md` edited to match in the same pull request:
   - a `**Kit:** method-v0.2` line and a `**Primary branch:** nigel/theseus` line under the title;
   - every objective as a `### Objective n.m: <title>` heading (objective 1.0's bullet becomes its heading) followed
     by its criteria as `- [ ]`/`- [x]` lines;
   - under every objective, phase 1's included, `**Pipeline:** <feature|bug|freeform>` and one workstreams table
     `| Workstream | Slug | Branch | Repos |`. Slug is the `work/<slug>/` folder; Workstream is a plain name, never a
     card id (P19, `PublicFilesTests`). Objective 0.2's prose line and 0.3's other table are rewritten to it;
   - `method/project.md`'s table, its rule for `<issue>` (now the workstream slug, also used in `nigel/<slug>`), and
     its Promotion section (which today "names the existing card") are updated to match.
   The reader reads the `Kit:` line. A file whose `Kit:` is `method-v0.1` shows as **pinned to method-v0.1** and is
   not read further; any other file that does not parse shows as **unreadable: <line and reason>**, never as empty or
   guessed. The reader has a test per shape and per refusal.
4. **Membership** (decision 2). A card belongs to at most one project and holds one current slug, recorded on this
   machine (two new card fields in the board file).
   - Set when Theseus creates the card for a project: `propose_workstream` gains an optional `slug`, and a proposal
     from a project workstream inherits its project; a verifier card takes its subject's project and slug (AC 10).
   - The owner can set or clear them from **Project… → <project> / <slug>** on the card's menu and the menu bar,
     offering the slugs the project file lists. The item shows only while at least one project is readable, so ad hoc
     cards on a board with no projects have no new element (AC 22).
   - A card with a project and no slug shows in the view under **No slug yet**; one whose slug is not in the file,
     under **Not in the project file**; a slug the file lists with no card, as **not started**.
   - Earlier slugs of the same workstream (several rows with one Workstream name) show in the tree at their own
     stage, not as cards.
5. **Where a slug's work is read** (decision 4): from the workstream's branch (the table's Branch column) while it is
   not merged into the primary; from the primary once it is. If neither branch is on the machine, the stage shows
   **not on this machine**. The source (branch and commit) is in the card's tooltip.
6. **Stages.** The view's columns are, in order: research, spec, plan, implement, verify, review, compound. A bug's
   stages sit under them as reproduce → research, rule → spec, fix → implement; bugs have no plan.
   **[default]** A stage is **reached** when its artefact exists: `research.md`, `spec.md`, `plan.md`,
   `implementation.md`, `evidence.md`, `review.md`, `lessons.md`; for bugs `reproduction.md`, then `fix.md` with a
   `## Spec` heading (rule), then `fix.md` with a `## Cause` or `## Change` heading (fix), then the same as a feature.
7. **Gates** (decisions 3 and 4A, departure 5). In a project the owner's gates are:
   - a workstream's **spec** (a bug's rule): passed only when he accepts it on the board (AC 15). Theseus records the
     acceptance in its own store with the commit of the spec it accepted, never from a file a session can write; if
     the spec changes after that, the gate shows **changed since accepted** and is waiting again;
   - the primary's **pull request into `main`**: passed when it is **merged**, read through objective 1.2's status
     (Q3: the merge is the real gate). Until 1.2 is built it shows **not read yet**; there is no Accept for it.
   Every other stage passes when the next stage is reached, except verify, which passes only when the last
   `theseus-evidence` block's `result` is `pass`; a workstream at verify with no `evidence.md` or no block shows
   **no evidence**, never passed. The evidence block's `gates` field is shown for information only (Q3).
   `feature.yaml` and `bug.yaml` mark plan and review as evidence gates inside a project in `method-v0.2`.
8. **Where a card sits.** At the furthest reached stage it may sit at: never past an owner gate that has not passed.
   If work has run past one (a `plan.md` written before the spec was accepted), the card sits at spec with **ahead of
   your gate: plan written** on it, so the waiting gate stays visible.
9. **Work from before method-v0.2.** A workstream whose slug's work was merged into the primary before the project
   moved to `method-v0.2` shows its reached stage with **before method-v0.2** and no gate markers, rather than every
   finished workstream showing its spec as waiting and its verify as no evidence. **[default]**
10. **The verifier card** carries its subject's project and slug, sits in **verify** in its subject's row beside it
    whatever the artefacts say, and leaves the row when it ends. Two cards may share a slug only when one is a
    verifier.
11. **Objectives done.** An objective is done when every criterion line is ticked. The current phase is the first with
    an objective not done.

### The project card and the view (pull request 2)

12. **The project card** sits on the board like any card, in a column the owner chooses (dragged like a card). It
    shows the project's name, current phase, n of m objectives done, and a waiting count: the project's sessions
    waiting for input, reports waiting for confirmation, proposals pending, and specs waiting. Hovering the count
    lists them.
13. **The project's cards leave the board's grid** (decision 1); the project card stands for them. A new card for the
    project adds to the count and appears at its stage in the view. The session drawer and the Sessions panel still
    list every live session. **If the project becomes unreadable, or its repository is removed from Settings, its
    cards return to the grid** until it reads again, so a broken parse never hides live work.
14. **Opening** the project card (double-click, Open on its menu, or View › Open project on the menu bar) replaces the
    board in the main area with the project view, with a breadcrumb **Board › <project>** back. The drawer, side
    panels and status bar stay. The view is its own user control, not more of `MainWindow.xaml`.
15. **Left: the tree** of phases, objectives (criteria ticked n of m) and workstreams, with each passed gate and
    anything waiting on the owner marked. Selecting an item scrolls the right-hand side to it. **Right: the
    pipeline**, a row per objective that has workstreams and the columns of AC 6. Cards have the same face and menu
    as on the board, and selection works as on the board (Ctrl and Shift, then Start selected and the other
    selected-card commands), so resuming a project's cards is one press. **Cards are not dragged in the view**: a
    stage comes from the artefacts. A spec gate that is reached and waiting shows **Waiting on you** between its
    columns, with **Open spec** and **Accept spec** (a confirmation names the slug and the commit). Every command in
    the view is also on the menu bar (rubric R16).
16. **Folding.** The view does not fold. Folding on the board is unchanged (decision 5).
17. **Name clash.** The board's existing rows-by-folder mode keeps its saved name (`Project`, so settings, row sizes
    and folds still read) and its **label** becomes **Folder**, so "project" on screen means only an Agentic Factory
    project. **[default]**
18. **A promoted ad hoc card** joins a project through AC 4's menu and keeps its key (decision 5); from then on it
    shows in the view and leaves the grid.

### The terminal window (pull request 3)

19. A project's cards open in one Windows Terminal window named `<terminal window name>-project-<project>`, where the
    first part is Settings' terminal window name (default `theseus`) and `<project>` is the project file's name
    without `.md`; the `-project-` keeps it apart from an area of the same name. In `WindowGroupName.For` it comes
    after a card's own window override and before area and column, so an override still wins. **[default]**
20. The window is chosen at each start, so a card that joins a project mid-session moves to the project's window at
    its next start, and resuming tomorrow opens it there. Each run records the window it was launched into.
21. **Bring the window forward** works through a live session whose run was launched into the project's window, as
    Focus does today; with none, it is disabled with the reason in its tooltip.

### Unchanged

22. With no project repositories set, or none readable, the board looks and behaves exactly as today, with no new
    menu item, checked against what is rendered (P11).

## Out of scope

Editing a project file from Theseus; drawing links; projects in repositories not listed in Settings; fetching from
remotes; Azure DevOps; Theseus starting or stopping project workstreams on its own (phase 2, autonomy proposal);
pull request and CI status (objective 1.2, which AC 7's PR gate reads once built).

## Holdout scenarios that apply

**Resume tomorrow** applies to pull request 3, which changes the window a resumed session opens in; its verify stays
open until a verifier card has run it (P18). A further candidate for Nigel to write: "a project's cards open in its
own window and resume there tomorrow".

## Review of draft 2, and what changed

Must-change: bug artefact names (AC 6 now `reproduction.md` and `fix.md`'s sections); owner gates with no way to pass
and a conflict with decision 4A (AC 7, departure 5); renaming the row mode would have reset every saved setting (AC 17
renames the label only); the checkout's branch was undefined (AC 2 reads with git from the branch). Should-change:
bring-forward could raise the wrong window (AC 20, 21); window name (AC 19); resume tomorrow applies (holdouts);
verifier card placement (AC 10); cards moving past unpassed gates (AC 8); work from before v0.2 (AC 9); proposals
carry no slug (AC 4); a new menu item on ad hoc cards (AC 4, 22); an unreadable project hiding its cards (AC 13);
menu bar and selection (AC 15); format edits and the `Kit:` line (AC 3); two missing departures (3 and 5) and where
the result block is delivered (PR 1). Consider: the PR gate now passes on merge, not on Accept (AC 7); earlier slugs
show their real stage (AC 4); window names cannot collide with areas (AC 19).
