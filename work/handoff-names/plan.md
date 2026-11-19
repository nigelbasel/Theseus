# handoff-names: plan

Pipeline: `feature`, stage **plan**, from `spec.md`. Overnight under Nigel's autonomy brief: the plan gate is taken as
the design he approved; recorded as overnight decision 7.

| # | Step | Files | Verified by |
|---|---|---|---|
| 1 | Tests first (new files) | `tests/Theseus.Tests/Stopping/HandoffOwnerTests.cs`, `HandoffRenameTests.cs`, `HandoffPointerTests.cs`; `tests/Theseus.Tests/Repository/RenameHandoffMenuTests.cs` | They fail before the code |
| 2 | `HandoffOwner.Of(file, sharers)`: the one sharer named by the file, or none (AC 1) | `src/Theseus.Core/Stopping/HandoffOwner.cs` | Step 1 |
| 3 | `HandoffPointer.Text(shared file, date)`: the new file's one line (AC 2) | `src/Theseus.Core/Stopping/HandoffPointer.cs` | Step 1 |
| 4 | `HandoffRename.Target(current file, title)` and `.Offered(...)` (AC 5) | `src/Theseus.Core/Stopping/HandoffRename.cs` | Step 1 |
| 5 | App: after the clash check, resolve handover clashes for cards with no live session (AC 2 to 4); set each card's offered rename (AC 5); `RenameHandoffCommand` on the card and its menu (AC 6) | `ShellViewModel.cs`, `WorkstreamCardViewModel.cs`, `MainWindow.xaml` | Live check |
| 6 | Full suite | | `dotnet test Theseus.slnx` |

**P21 (code moved out of a sequence):** none moved. **P23 (a restriction added):** none added; a file is created or
moved only for a card with no live session, so no running session is told one path and finds another.

**Live check:** the pair of cards on Nigel's board that share a chosen file resolves (the card the file is named after
keeps it; the other gets `HANDOFF-<its key>.md` with a pointer line); a card whose handover file is named for an old
key offers the rename when its session is not running.
