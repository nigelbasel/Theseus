# The project format

A project is a Markdown file in `projects/` of its **home repo**: the repo where its objectives are
written and its artefacts are kept. It is read by people and by agents, so it is prose with a fixed shape,
not a schema to fill in. Standalone (ad hoc) workstreams have no project file.

## Shape

```markdown
# <Project title>

## Objective
One paragraph: the outcome the whole project exists for.

## Acceptance criteria
- [ ] Observable facts that mean the objective is met. Evidence, not dates.

## Repos
| Repo | Role | Autonomy |
|---|---|---|
| <host>/<owner>/<repo> | home | L0 |
| <host>/<owner>/<other-repo> | changed by phase 2 | L0 |

## Phase <n>: <name>
**Exit gate:** who decides, on what evidence.

### Objective <n.m>: <name>
What this part of the work must achieve.
- [ ] Its acceptance criteria.

**Pipeline:** feature | bug | freeform
**Workstreams:**
| Card | Branch | Repos | Artefacts |
|---|---|---|---|
| <card-slug> | <user>/<feature> | <repo>, <other-repo> | work/<issue>/ |

## Decisions
| Date | Decision | By |
```

## Rules

- **The objective is the project's.** Phases break it down; each phase's derived objectives have their
  own acceptance criteria, and the phase has one exit gate. A phase is done on evidence, not on a date.
- **A workstream serves exactly one derived objective.** Several may serve the same one at once. They
  coordinate as Theseus already does: links for dependencies, locks for shared resources, and overlap
  detection on the files they change.
- **A workstream may span repos.** It uses one branch name in every repo it changes, with one worktree per
  repo, all under one feature folder: `<root>\<feature>\<repo>` on `<user>/<feature>`. Nothing in a
  pipeline or artefact path names a single repo.
- **Artefacts live once, per workstream, in the home repo**, under `work/<issue>/`, whichever repos the
  code changes are in. `<issue>` is the tracker item the workstream is for (a GitHub issue number or a
  tracker key), or the card slug when there is none.
- **Evidence is per repo.** Where a stage needs proof (tests passed, a scenario passed), it records one
  result for each repo the workstream changed. A repo with no result has no evidence.
- **Autonomy is per repo.** The table under Repos sets each repo's level. A workstream runs at the lowest
  level of the repos it changes.
- **Decisions are recorded in the file**, dated and attributed, not only in a session.

## Promotion

Ad hoc work becomes a project by writing a project file whose first phase names the existing card as its
workstream. The card's handoff is linked as the project's first document. Nothing about the card changes
until it next starts a stage of a pipeline.

## Example

[`projects/theseus-bootstrap.md`](../projects/theseus-bootstrap.md), the Theseus bootstrap itself.
