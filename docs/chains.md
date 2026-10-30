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
