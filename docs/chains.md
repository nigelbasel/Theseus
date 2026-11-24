# Chains and proposals

[Help home](index.md)

A **chain** is work that passes from one workstream to the next: analysis, then design, then
implementation, each in its own workstream with its own session.

## Links

Two cards are joined by a link.

- **Hands over**: the parent hands its work to the child, which carries it into the next stage. A child
  has one parent. A parent may have several children.
- **Reports back**: the child reports its own result back to its parent when it is done.

A link can name a document: the hand-over the parent writes, or the report the child writes. A child
waiting on a hand-over document is held back from starting until you accept the document.

Make links from a card's Properties, on its **Links** tab, or with **Spawn follow-on** on the card's
right-click menu, which creates the next workstream already linked.

## The Chains panel

The chain button on the toolbar opens the Chains panel. It shows:

- **Proposed**: new workstreams that sessions have proposed, waiting for you.
- **Needs you**: reports waiting for you to confirm, and links waiting on a document.
- **Ready**: cards whose inputs have arrived and can start.
- The whole tree of linked cards.

## What a session can do

A session can see its links, and when you ask it in its tab it can also act on them. Claude Code asks
your permission before each of these.

| Ask the session to… | It uses |
|---|---|
| Show what this workstream is linked to | `related` |
| File the document a link asks for | `report_back`. The link then waits for you to confirm the report on the board. |
| Link to another card, as parent or child | `link_workstream` |
| Remove one of its links | `unlink_workstream` |
| Propose a new workstream for part of the work; in a project, create it at once under an objective | `propose_workstream`, after writing a brief for it |
| Show its proposals | `proposal_status` |
| Approve or reject one of its proposals | `approve_proposal` or `reject_proposal` |
| List the cards it may act on, with their state, why each is stopped, who made it and who paused it | `workstreams` (runs without asking) |
| Start one of those cards, with an instruction for that start only | `start_workstream` |
| Stop one of those cards' sessions | `stop_workstream` |
| Pause one of those cards, with a reason; its project's Start leaves it alone | `pause_workstream` |
| Clear a pause on one of those cards and start it (in a project, any session's pause; otherwise its own) | `resume_workstream` |
| Show its spec's gate: the version on disk, and whether, when, by whom and how you accepted it | `gates` (runs without asking) |
| Report that you accepted its spec in its tab, quoting you | `accept_spec`. Nothing is accepted until you confirm on the board |
| Show what you did on the board that concerns it since it last asked | `events` (runs without asking) |

**What you do on the board, its session is told.** Approving or rejecting a proposal tells the card that proposed it;
confirming a report or a hand-over tells the card it was for; accepting a spec tells the card that wrote it. A running
session is told with its next turn, as a stop request is; `events` lists them all. Accepting a spec also starts its card
if it is stopped, told to carry on to the plan. In a project, confirming a report also starts the card it was for if it is
stopped, unless you paused, blocked or archived it; on the ordinary board confirming only tells.

**In a project**, a session acts on every card of the project but its own, by membership alone: never another
project's card or a card in no project, and a card moved out stops being its at once. It may take over or clear another
session's pause. A card **you** paused is not started: the session's start becomes a line in the project's **Waits for
you**, "<session> wants to start <card>, which you paused: <reason>", with **Allow or refuse…**. Allow clears your
pause and starts it; either answer is told to the session. The request lapses if you start or resume the card yourself,
or it leaves the project or is archived.

**On the ordinary board**, a session acts only on its **descendants**: cards made from proposals its card raised and
you approved, and theirs in turn. Theseus works this out from its own records, not from what the session says, so it
can never reach your other cards, another project's, or the card that proposed it. It cannot start a card that is
paused by you.

Either way it cannot start a card that is running, archived or blocked. Each call shows in the status bar and in the card's **History** ("Restarted by Theseus: more work: …").
A child that reports back stops once its report is in; its parent restarts it with an instruction when there is more.
Claude Code asks you before each of the four that act, unless the session runs in a mode that approves tools by itself.

Sessions also call `record_work_item` when they start on a Jira issue, raise one, or open a pull
request, so the card's drop-downs can show its title. That one runs without asking, because it only
annotates the session's own card.

## Proposals

A session that thinks part of its work belongs in a new workstream writes a **brief** (what to do,
what to read, what is out of scope) and proposes it.

**In a project the card is made at once**, without your approval: it must name an objective already in the project
file and its records folder, its row is written under that objective, and its History says which session created it.
It starts only if the session asks. There is no limit on how many a session creates. You review the objectives and
their specs; the cards are the sessions'.

**On the ordinary board** nothing exists yet: no card, no link and no tab. You decide in either place:

- **On the board**: **Review…** in the Chains panel, **Review…** in Waits for you, or **Approve…** in the
  project view opens the **proposal window**: what is proposed, under which objective, its brief drawn, what "done"
  means and its warnings. Nothing in it is editable; the session answers for the brief. **Approve** makes the card,
  **Reject…** takes an optional note, **Cancel** decides nothing.
- **In the session's tab**: ask the session to show you the proposal. It reads you the brief, what
  "done" means and any warnings. Then say approve or reject.

Approving creates the card and its links. It never authorises a Jira item, a branch, a worktree, a push
or a pull request, except what a project's branch rules grant; the new session is told to ask you before any other.

Theseus refuses a proposal whose brief is missing, that points at another workstream's handoff, or that
names a Jira issue the proposing card does not hold. On the ordinary board a workstream can have at
most three proposals waiting, and a child may propose but a grandchild may not.
