# Reviewer rubric: Theseus

What a reviewer checks a Theseus change against. Every item comes from a dated decision recorded in the
code, and cites where. A change that breaks an item is a finding unless the owner has decided, in a dated
comment in the same change, to supersede that decision.

How to use it: for each item, decide **holds**, **breaks** (cite the line) or **not touched**. Report only
breaks and doubts. An item without a source is not a rubric item; propose it as a lesson instead.

## Authority and trust

**R1. Anything that changes the board passes the person in the tab first.**
A report, a proposal or its withdrawal, a link made or removed, and an approval or rejection are not
pre-approved tools. Pre-approved tools are named one by one, never with a wildcard, so a tool added later
is not approved without a decision.
*Breaks if* a board-changing tool joins the pre-approved list, or the list gains a wildcard.
Source: `src/Theseus.Core/Mcp/TheseusMcp.cs` (decision 10, 2026-10-21; Nigel, 2026-09-17 and 2026-10-06).

**R2. A report is a claim.**
A session's report changes nothing that matters: its link shows Reported until the owner confirms it on
the board.
*Breaks if* any state changes as a result of a report alone.
Source: `src/Theseus.Core/Mcp/McpHandler.cs`; `src/Theseus.App/ViewModels/ChainsViewModel.cs` (Nigel, 2026-10-21).

**R3. Nothing starts a session on its own.**
Ready is shown and counted, never acted on. The chains view is read-only.
*Breaks if* any code path launches a session without a person pressing Start, except a project workstream started by
Theseus inside a project at autonomy level L1 or above, for an objective in its approved project file (Nigel,
2026-11-20; none exists until the levels are built in phase 2), and a session starting one of its own descendants with
`start_workstream` or `resume_workstream`, a tool call that passes the person in the tab (Nigel, 2026-11-21, 1.1e AC 14),
and a stopped card started by the owner's Yes to Accept spec…, whose question says it will be started (1.1e AC 27,
Nigel's spec, 2026-11-21).
Source: `src/Theseus.App/ViewModels/ChainsViewModel.cs` (Nigel, 2026-10-21); `src/Theseus.App/MainWindow.xaml`, chains (Nigel, 2026-10-23).
Descendants: `src/Theseus.Core/Proposals/WorkstreamDescendants.cs`, `src/Theseus.Core/Mcp/McpHandler.cs` (Nigel, 2026-11-21).

**R4. Theseus reads trackers and never writes them; sessions never report durations.**
Time comes from the attention sampler, attended and machine time separately, and the person logging chooses.
*Breaks if* Theseus gains a tracker write path or credential, or a tool accepts a duration from a session.
Source: `src/Theseus.Core/Mcp/McpHandler.cs` (Nigel, 2026-09-17).

## Locks and clean-up

**R5. Only the owner reorders, cancels or releases someone else's lock, from the board.**
No session tool can take, reorder or cancel another session's ticket. Release asks twice, because it does
not stop the holder's run.
*Breaks if* a session-facing tool or HTTP verb can act on another's ticket.
Source: `src/Theseus.Core/Locks/LockBook.cs`, `src/Theseus.App/ViewModels/LocksViewModel.cs`, `src/Theseus.App/MainWindow.xaml`, locks panel (Nigel, 2026-10-06).

**R6. The lock book survives a restart and refuses unknown locks.**
Every change is saved, so a restart resumes exactly where it stopped; every grant and release is recorded.
A lock name outside the list is refused, not created. An unreadable state file is logged and treated as
empty, never fatal to the board.
*Breaks if* state is held only in memory, a typo can create a lock, or a bad file stops the board.
Source: `src/Theseus.Core/Locks/LockBook.cs`, `LockNames.cs`, `LockStateStore.cs` (Nigel, 2026-10-06).

**R7. Clean up only what can be proved to be the dead run's.**
A test host must still have its recorded start time; a container must carry the Testcontainers label and
have been created inside the hold. What cannot be proved is shown for the owner to remove, never removed
automatically.
*Breaks if* clean-up matches by name, image or age alone, or removes an unowned container.
Source: `src/Theseus.Core/Locks/OrphanCleaner.cs`, `UnownedContainers.cs` (Nigel, 2026-10-06).

**R8. Scripts and hooks that cannot use MCP get plain HTTP, and a token goes only to its requester.**
*Breaks if* a listing exposes another ticket's token.
Source: `src/Theseus.Core/Locks/LockHttpApi.cs` (Nigel, 2026-10-06).

## Sessions

**R9. Nothing destructive happens at startup without asking.**
A stop left half-finished is not completed silently on the next launch: it waits, naming the sessions, until
the owner says.
*Breaks if* launching the board can end or alter a session with no confirmation.
Source: `src/Theseus.App/ViewModels/ShellViewModel.cs`, `src/Theseus.App/MainWindow.xaml` (Nigel, 2026-09-02).

**R10. Do not be silent about state the user can see.**
Starting a card that already has a tab with nothing running in it opens the new tab and says so on the board
(`StartNotice.OldTabLine`); it no longer asks first, which made every Start two presses (Nigel, 2026-11-23).
*Breaks if* a Start can open a second tab for a card without saying so.
Source: `src/Theseus.Core/Launching/AbandonedTabs.cs`, `src/Theseus.App/ViewModels/ShellViewModel.cs` (Nigel, 2026-09-07, 2026-11-23).

**R11. Do not start a session that has nothing to read.**
Cards still waiting on an input are held back and asked about, for one card or a selection.
*Breaks if* a session can start before the document it is waiting on exists, without asking.
Source: `src/Theseus.Core/Links/WaitingStarts.cs`, `src/Theseus.App/MainWindow.xaml` (Nigel, 2026-10-21).

**R12. Sessions learn Theseus's state from a file, because a stopped Theseus can tell them nothing.**
*Breaks if* sessions depend on Theseus being up to learn it is down, or are left with a bare connection error.
Source: `src/Theseus.Core/Status/TheseusStatusNotice.cs`, `src/Theseus.App/App.xaml.cs` (Nigel, 2026-10-30).

**R13. Paused and blocked are marks on a card, not places, and starting a marked card asks.**
Clear marks and start clears the mark and starts; Start only the others, or Cancel, leaves it marked and unstarted.
*Breaks if* pausing moves a card out of its stage, or a marked card starts without the question.
Source: `src/Theseus.Core/Domain/WorkstreamSidecar.cs`, `src/Theseus.Core/Launching/FlaggedStart.cs`, `src/Theseus.Core/Domain/BoardTidy.cs` (Nigel, 2026-10-30).
One exception: `resume_workstream` clears and starts a card paused by the calling session itself, through a tool call
that passes the person in the tab; it never touches the owner's pause, another session's, or a blocked card (Nigel,
2026-11-21, 1.1e AC 14). Source: `src/Theseus.Core/Mcp/McpHandler.cs`.

**R14. Briefs and reports are placed relative to the spawning workstream, never by absolute path.**
*Breaks if* a brief or report path is stored absolute.
Source: `src/Theseus.Core/Settings/TheseusSettings.cs` (Nigel, 2026-10-21).

## Updates and secrets

**R15. Nothing secret is needed to run or update; updates are offered, never forced.**
Releases are read from the public repo with no token. Nothing downloads or installs until Update is
pressed; Later sets a version aside until a newer one appears.
*Breaks if* a token is added for updates, or anything installs without the user's press.
Source: `src/Theseus.Core/Updates/GitHubReleaseFeed.cs`, `src/Theseus.App/ViewModels/UpdateViewModel.cs` (Nigel, 2026-10-28).

## The Desk

**R16. Conventional Windows behaviour.**
A menu bar holding every command, an icon toolbar for the frequent ones, a status bar for readings; menus
spaced as normal desktop menus; transport-style icons (play, stop, pause) with a tooltip each; click,
Ctrl+click and Shift+click select as in Explorer, and a card's menu acts on the selection when the card is in it.
*Breaks if* commands appear only outside the menu bar, word buttons return to the toolbar, or selection
behaves differently from Explorer.
Source: `src/Theseus.App/MainWindow.xaml`, `src/Theseus.App/Themes/CompactMenu.xaml`, `src/Theseus.App/ViewModels/BoardViewModel.cs`, `ShellViewModel.cs` (Nigel, 2026-10-30).

**R17. Live state applies as it changes; rarely changed setup has its own dialog.**
The detail drawer has no Save, so nothing half-made is lost. A card's setup and New workstream are dialogs
of their own, with Save/Create and Cancel always visible, Escape as Cancel, and Ctrl+Enter as Create where
Enter is a new line.
*Breaks if* an edit can be lost without a word, or a dialog's buttons can scroll off screen.
Source: `src/Theseus.App/ViewModels/DetailDrawerViewModel.cs`, `src/Theseus.App/SetupWindow.xaml` (Nigel, 2026-10-22); `src/Theseus.App/NewWorkstreamWindow.xaml` (Nigel, 2026-11-03).

**R18. A card does not grow without bound.**
Lists such as issues and pull requests are drop-downs, not chips.
*Breaks if* a card's height grows with the number of items it holds.
Source: `src/Theseus.App/MainWindow.xaml`, card items (Nigel, 2026-10-30).

**R19. Each lock entry carries its workstream's colour, matched exactly or not at all.**
*Breaks if* a colour is guessed for a ticket that matches no card.
Source: `src/Theseus.App/ViewModels/LockColours.cs` (Nigel, 2026-10-06).

## Not in this rubric

House rules that are not dated decisions (one type per file, warnings as errors, test naming) are in
`AGENTS.md`, and the build enforces some of them. Lessons that should become rubric items are proposed by
the compound stage and added only with a source.
