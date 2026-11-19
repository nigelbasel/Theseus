# handoff-names: implementation

Pipeline: `feature`, stage **implement**, from `plan.md`.

| AC | Where |
|---|---|
| 1 | `HandoffOwner.Of`: the sharer whose key or title-as-key the file name contains; when several, the one whose match contains every other's; otherwise none |
| 2, 3, 4 | `ShellViewModel.ResolveSharedHandoffs`, run on every refresh before the clash check: for each other sharer with no live session, `HandoffPointer.OwnFile` is created with `HandoffPointer.Text` if absent (never overwritten), set as the card's handover file and persisted; logged and shown in the status bar. A write failure is logged and the card left as it was |
| 5 | `HandoffRename.Offered`, set on each card as `HandoffRenameTo` after the clash check; the card menu item is shown only when it is set |
| 6 | `ShellViewModel.RenameHandoff`: re-checks the offer, one `File.Move` without overwrite, then sets and persists the card's handover file |
| 7 | Keys are untouched |

Full suite: **1634 of 1634** (1621 before, 13 new).

Live check (for Nigel, after installing): the "NDC MCP Server" card gets `HANDOFF-ndc-mcp-server.md` beside the shared
file, starting with the pointer line, and its warning goes; the Theseus card's menu offers "Rename handover to
HANDOFF-theseus.md" while its session is stopped.
