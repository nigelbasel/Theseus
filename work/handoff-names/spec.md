# handoff-names: spec

Pipeline: `feature` (method kit v0), stage **spec**, autonomy L0. On branch `nigel/verifier-card` (Nigel's choice,
2026-11-19, option A), repo Theseus. **The design was approved by Nigel on 2026-11-19** as proposed in the
conversation ("you can do it"); this records it.

## What changes, from the owner's side

Two cards no longer stay pointed at one handover file: the card the file is named after keeps it, and each other card
gets a file of its own, which starts by pointing at the old one, so nothing is lost or overwritten. And when a card is
renamed, its menu offers to rename its handover file to match.

Research (overnight, measured on the board's files): of 69 live cards one pair shared a file, and both had chosen it
explicitly; Theseus's own search did not cause it. A card keeps the handover name it got when it was created
(`HANDOFF-<key>.md`), whatever its title becomes.

## Acceptance criteria

**A shared handover file**

1. When cards share a handover file, the owner is the one whose key, or whose title written as a key, appears in the
   file's name. If that is not exactly one card, nothing changes and the card shows today's warning.
2. Each other sharing card, while no session of it is running, is given `HANDOFF-<its key>.md` in the shared file's
   folder as its handover file. If that file does not exist it is created, holding one line that names the shared
   file and the date. An existing file is never overwritten.
3. A card whose session is running is left as it is until the session ends: its running session was told the old path.
4. Each move is logged and shown in the status bar, naming both cards and both files.

**Renaming with the title**

5. A card whose handover file's name does not match its title (`HANDOFF-<title as a key>.md`) offers **Rename
   handover to HANDOFF-<title as a key>.md** in its menu, only while no session is running, only when that name is free
   in the folder, and only when its handover file exists.
6. Choosing it moves the file to the new name and makes it the card's handover file. Nothing is copied or deleted; it
   is one move.
7. The card's key is unchanged.

## Out of scope

Renaming card keys; automatic renaming without the owner's choice; handover files that name no card.

## Holdout scenarios that apply

None.

## Record

| Stage | Started | Finished |
|---|---|---|
| spec | 21:52 (logged) | this commit's time |
