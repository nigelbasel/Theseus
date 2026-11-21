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
| Propose a new workstream for part of the work | `propose_workstream`, after writing a brief for it |
| Show its proposals | `proposal_status` |
| Approve or reject one of its proposals | `approve_proposal` or `reject_proposal` |
| List the cards made from its approved proposals, and theirs in turn, with their state and why each is stopped | `workstreams` (runs without asking) |
| Start one of those cards, with an instruction for that start only | `start_workstream` |
| Stop one of those cards' sessions | `stop_workstream` |
| Pause one of those cards, with a reason; its project's Start leaves it alone | `pause_workstream` |
| Clear its own pause on one of those cards and start it | `resume_workstream` |

A session acts only on its **descendants**: cards made from proposals its card raised and you approved, and theirs in
turn. Theseus works this out from its own records, not from what the session says, so it can never reach your other
cards, another project's, or the card that proposed it. It cannot start a card that is running, archived, blocked or
paused by you. Each call shows in the status bar and in the card's **History** ("Restarted by Theseus: more work: …").
A child that reports back stops once its report is in; its parent restarts it with an instruction when there is more.
Claude Code asks you before each of the four that act, unless the session runs in a mode that approves tools by itself.

Sessions also call `record_work_item` when they start on a Jira issue, raise one, or open a pull
request, so the card's drop-downs can show its title. That one runs without asking, because it only
annotates the session's own card.

## Proposals

A session that thinks part of its work belongs in a new workstream writes a **brief** (what to do,
what to read, what is out of scope) and proposes it. Nothing exists yet: no card, no link and no tab.

You decide in either place:

- **On the board**: **Review…** in the Chains panel opens the new-workstream dialog filled in from the
  proposal. Change anything you like; **Create** approves it. **Reject…** takes an optional note.
- **In the session's tab**: ask the session to show you the proposal. It reads you the brief, what
  "done" means and any warnings. Then say approve or reject.

Approving creates the card and its links. It never authorises a Jira item, a branch, a worktree, a push
or a pull request; the new session is told to ask you before any of those.

Theseus refuses a proposal whose brief is missing, that points at another workstream's handoff, or that
names a Jira issue the proposing card does not hold. A workstream can have at most three proposals
waiting, and a child may propose but a grandchild may not.
