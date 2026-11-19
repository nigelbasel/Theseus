# handoff-names: review

Pipeline: `feature`, stage **review**, by an independent reviewer session (not the implementer). Change reviewed:
commits `51176f5` and `d82c485` on `nigel/verifier-card`, judged against `spec.md` and `.agents/reviewer-rubric.md`.

| Started | Finished |
|---|---|
| 22:02:02 (from `date` at the start of the review) | 22:12 (from `date` before this file was written: 22:08:05, plus writing) |

Checked: the diff, `HandoffPath`, `WorkstreamKey`, `CardLifecycle`, `ShellViewModel.Refresh`/`SyncLifecycles`/`StartMany`,
`BoardViewModel.Refresh`/`Persist`, `LaunchCoordinator.Launch`, `App.xaml.cs` refresh triggers. Ran the 13 new tests
with a filter: 13 of 13 pass. The full suite was not run.

## Must-fix

### M1. A card that has just been started is moved before its session is seen (AC 3)

`src/Theseus.App/ViewModels/ShellViewModel.cs:1556` (`!card.IsLive`), with `StartMany` at `:1885`-`:1936` and
`CardLifecycle.For`.

`StartMany` pins the card (its `StatusDocumentPath` is already the shared file), `LaunchCoordinator.Launch` bakes that
path into the opening prompt (`LaunchCoordinator.cs:132`), and `StartMany` then calls `Refresh()`. At that moment the
session's registry entry does not exist yet (it is written by the session's hook seconds later), so
`SyncLifecycles` computes `live = false` and overwrites the `Launching` state the launch sink set; `IsLive` is false.
`ResolveSharedHandoffs` then moves the card to `HANDOFF-<key>.md`. The new session was told the shared file and will
write its handover there, while the board and every later stop name the new file. This is exactly what AC 3 forbids.
The same window lets the rename menu item (`SyncHandoffRenames`, `:1592`; `RenameHandoff`, `:1605`) be offered and
acted on for a card whose tab is opening.

Fix: treat a card as running for these two features when it has a launch in flight, not only a registry entry: for
example a card whose `LastStartedUtc` (or a run in the run log with no ending) is within the launch ticket's lifetime
(`WorkstreamLaunchPlanner.Plan(..., UtcNow.AddMinutes(2))`), or a card with an outstanding launch ticket. Put that
predicate in Core and test it.

### M2. The status bar does not name both cards and both files (AC 4)

`src/Theseus.App/ViewModels/ShellViewModel.cs:1580`.

AC 4: "Each move is logged and shown in the status bar, naming both cards and both files." The log line does
(`:1577`). The notice names only the moved card and the new file name: "X has its own handover file now,
HANDOFF-x.md: it was sharing one with another card." The owner card and the shared file are missing.

Fix: name the owner's title and both file names, e.g.
`$"{card.Title} now keeps its handover in {Path.GetFileName(own)}; {ownerTitle} keeps {Path.GetFileName(group.Key)}."`
(the owner's title is available from the group). Put the text in Core (e.g. on `HandoffPointer`) so a test can check
it against the AC.

### M3. Employer and internal system names, and a real card key, in a public repo

- `tests/Theseus.Tests/Stopping/HandoffOwnerTests.cs:9`, `:16`-`:18`: "configure-the-nurtur-ndc-mcp-server-in-the-claude-directory",
  "Configure the Nurtur NDC mcp server…", "NDC MCP Server".
- `src/Theseus.Core/Stopping/HandoffOwner.cs:7`-`:8`: "…-ndc-mcp-server-in-the-claude-directory", "ndc-mcp-server".
- `tests/Theseus.Tests/Stopping/HandoffPointerTests.cs:19`-`:20`: "ndc-mcp-server".
- `src/Theseus.Core/Stopping/HandoffRename.cs:7` and `HandoffRenameTests.cs:12`, `:25`, `:34`, `HandoffOwnerTests.cs:25`-`:26`:
  `git-22`, the key of a real card on the owner's board.
- `work/handoff-names/plan.md:18`-`:19` and `implementation.md:15`: "NDC MCP Server", "the NDC MCP pair on Nigel's board".

AGENTS.md, "The repo is public": no names of employers' internal systems. `git grep -i nurtur main` finds nothing
today, so this change introduces it. No automated public-files test exists in this worktree to catch it.

Fix: replace with invented placeholders (`set-up-the-ledger-server-in-the-directory` / `ledger-server`;
`old-key` / "Alpha"), and describe the live check in the records without the real card names (they can stay in the
owner's private notes).

## Should-fix

### S1. The owner rule goes beyond AC 1 as written

`src/Theseus.Core/Stopping/HandoffOwner.cs:29`-`:33`.

AC 1: "the owner is the one whose key, or whose title written as a key, appears in the file's name. If that is not
exactly one card, nothing changes." In the motivating pair both keys appear in the file name (`ndc-mcp-server` is a
substring of the longer key), so the AC as written says nothing changes. The code adds a tie-break the spec does not
contain: the card whose match contains every other's wins. The plan's live check expects the tie-break, so the
intent is probably right, but the rule was decided by the implementer, not recorded as the owner's.

Fix: put the containment rule in AC 1 with the owner's approval (a spec change), or drop it. Either way the spec and
the code must say the same.

### S2. Substring matching makes false owners

`src/Theseus.Core/Stopping/HandoffOwner.cs:23` (`name.Contains(token)`).

A key or title-key matches anywhere inside the name, not at word boundaries. `HANDOFF-latest.md` shared by a card
keyed `test` and another card: `test` "appears", so the `test` card is made owner and the other card is moved,
although the file is not named after either. Short keys (`mcp`, `ui`, `api`) match a large share of names. An empty
title becomes `workstream` (`WorkstreamKey.FromTitle`), which matches any name containing that word.

Fix: match on hyphen boundaries: `($"-{name}-").Contains($"-{token}-")` after stripping the `handoff-` prefix, and
add a test with a key embedded inside another word.

### S3. Creating the new file re-points other cards that find their notes by discovery (cascade)

`src/Theseus.App/ViewModels/ShellViewModel.cs:1564`-`:1569`, interacting with `HandoffPath.Discover`
(`HandoffPath.cs:62`-`:69`).

A card with no `StatusDocumentPath` gets its handover from `Discover`, which takes the most recently written file in
its folders whose name contains its key or title as a substring. The pointer file just written is the newest file. A
third card keyed, say, `ledger` that was discovering `HANDOFF-ledger-notes.md` now discovers the new
`HANDOFF-ledger-server.md` instead, silently: its card now points at a one-line pointer to a file that holds none of
its notes, and on the next refresh it shares that file with `ledger-server`, is moved again, and gets
`HANDOFF-ledger.md` pointing at the pointer. The notes are not deleted, but the card has lost track of them, which is
what the spec's "nothing is lost" is meant to prevent. The same happens when `own` already exists as another card's
chosen handover: the moved card adopts that card's file and a new share is created, and the next refresh moves the
other card too.

Fix (any of): before writing, pin the discovered path of every card in the same folders (`HandoffPin`) so a new file
cannot change their answer; skip and warn when `own` is already any card's handover in `handoffs`; or limit
resolution to sharers whose `StatusDocumentPath` was chosen (the only case the research found).

### S4. Rename moves a file another card is still using

`src/Theseus.Core/Stopping/HandoffRename.cs:17`-`:25`; `ShellViewModel.cs:1587`-`:1595`, `:1603`-`:1633`.

The offer checks only the card's own session. When a file is still shared, because the other sharer is live (AC 3)
or because no owner could be decided (AC 1), each sharer whose title does not match is offered the rename. Choosing
it moves the file out from under the other card: a live session there was told the old path and writes a fresh file
at it, and a discovering card loses the file. This undoes AC 3's protection by a different route.

Fix: offer the rename only when no other card's handover (from `handoffs`, compared case-insensitively) is the same
file; re-check in `RenameHandoff`.

### S5. A pointer file can be written into a repository working tree

`src/Theseus.Core/Stopping/HandoffPointer.cs:12`-`:13`, `:16`-`:18`; `ShellViewModel.cs:1566`.

The shared file may be one that was discovered in a card's working directory or one level below it
(`HandoffPath.Roots`/`SearchedDirectories`), which is often a repo worktree. The new `HANDOFF-<key>.md` lands beside
it, untracked, and its text holds the absolute path of the shared file. A later `git add -A` in that repo commits a
card key and a machine path, which in a public repo breaks "The repo is public". It is created automatically, with no
one asked, on any refresh including start-up.

Fix: do not create the file when its folder is inside a git working tree (a `.git` directory or file in it or an
ancestor) unless the shared file is itself tracked-ignored there; leave the warning and log why. Writing only the
shared file's name, not its full path, when both are in the same folder also removes the machine path.

### S6. A failed write is logged on every refresh

`src/Theseus.App/ViewModels/ShellViewModel.cs:1571`-`:1575`.

`Refresh` runs on every session registry change (`App.xaml.cs:156`), on links changes, and after most commands. When
the folder is read-only or gone, the same Warning is written on every one of those refreshes for as long as the share
lasts.

Fix: remember the (card, file) pairs already warned about in this run and log once; or show it once as a notice.

### S7. The App wiring (AC 2, 3, 4, 6) has no test, and two Core mutants survive

- `HandoffOwnerTests`: a mutant that ignores the **key** and uses only the title survives (in every test the deciding
  card's title-as-key equals its key, or the title decides). Add: file `HANDOFF-old-key.md`, sharers
  `("old-key", "Something else")` and `("other", "Other")`, owner `old-key`.
- A mutant `owners.Count >= 1 ? owners[0].Key` survives: no test has two sharers with identical matches. Add: sharers
  `("alpha", "Beta")`, `("alpha-2", "Alpha")` on `HANDOFF-alpha.md` → null.
- Nothing tests `ResolveSharedHandoffs` or `RenameHandoff`. Minimal cover without a window: move the decision into
  Core, e.g. `SharedHandoffs.Plan(IReadOnlyList<(string Key, string Title, string Handoff, bool Running)> cards,
  Func<string, bool> exists, DateOnly today)` returning `(Key, From, To, CreateWithText?, Notice)` moves, and test:
  the owner is not moved (AC 1); a non-owner gets `HANDOFF-<key>.md` in the shared folder with the pointer text
  (AC 2); an existing file gets no text (never overwritten, AC 2); a running card is not moved (AC 3); the notice
  names both cards and both files (AC 4); no owner → no moves. For AC 6, a Core `HandoffRename.Apply(current, target)`
  tested on a temp directory: the file is at the target with the same content, nothing is at the old path, and an
  existing target is refused and left untouched. The App then only applies the plan and persists.

## Consider

- **C1. Archived cards are resolved when the board shows the archive.** `Board.Cards` is the current view only
  (`BoardViewModel.cs:150`, `IsArchived == ShowArchived`), so with Archived shown, archived cards that share a file are
  moved and files written for them; with it hidden, an archived sharer is invisible to the rule. The spec is silent;
  decide whether archived cards should be left alone (likely) and skip `IsArchived` explicitly.
- **C2. Cards filtered out of the view are not seen.** Same cause: a sharer hidden by a view or area filter is not part
  of the group. Harmless today (nothing moves until it is visible) but the outcome depends on what is on screen. Using
  the full workstream list rather than the view would make it deterministic.
- **C3. A session running unattached to its card is not `IsLive`.** A card with a lost (unadopted) session
  (`LostSessionName`) can be moved while that session runs with the old path. Include it in the running check.
- **C4. The status bar keeps only the last notice.** Several moves in one refresh, or a command that sets `Notice` after
  calling `Refresh()` (archive, delete), leave only one visible. `Notices` keeps the history, so nothing is lost;
  a single combined notice ("2 cards have their own handover files now: …") would read better.
- **C5. File IO on the UI thread.** `SyncHandoffRenames` adds two `File.Exists` per card per refresh (about 140 on a
  69-card board), on top of `HandoffPath.For`'s directory enumeration, all on the dispatcher. Cheap on a local disk,
  noticeable on a network share. Pre-existing pattern; worth noting rather than fixing here.
- **C6. Title-to-key differs from `HandoffPath`'s own slug.** `HandoffOwner` and `HandoffRename` use
  `WorkstreamKey.FromTitle` (ASCII only, separators collapsed); `HandoffPath.Names` uses `Slug` (any letter, each
  separator kept, case-insensitive). A title such as "Ledger: MCP" is `ledger-mcp` to one and `Ledger--MCP` to the
  other, so a file the board discovers for a card by title may not make that card its owner, and vice versa. AC 1 says
  "title written as a key", so the new code is right; `Slug` is the odd one out.
- **C7. Rename acts on one card while the menu convention acts on the selection** (R16). Reasonable for a per-file
  action like Properties; say so in the tooltip or the doc comment if it is deliberate.
- Style: `var`, braces, one type per file and XML-doc comments citing the decision all match `AGENTS.md`. The
  `RenameHandoffMenuTests` file sits in `Repository/` beside `VerifyMenuTests`, matching existing placement.
