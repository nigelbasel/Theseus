# Duties and the overnight queue

[Help home](index.md)

A **duty** is an instruction Theseus gives a card again and again on a cadence, such as "check for new bugs and update
the register" every two hours. Without duties, a session that should look at something periodically has to run a loop
in its own tab. That loop dies with the session or with a restart, keeps the tab busy, and nobody else can see it,
pause it or change it. Theseus keeps duties itself, so they survive a restart of Theseus or of the machine.

## When a duty runs

A duty runs **every** so often (at least every 5 minutes), counted from when it was set, or **at** set times of day,
optionally on chosen weekdays only. When it is due, the card's state decides what happens:

| The card is | What happens |
|---|---|
| Open and idle | The instruction is handed in as its next turn. |
| Not running | Its session is started with the instruction. |
| Working | The run waits until the turn ends. |
| Asking you something | The run waits. Nothing is typed into a session that is waiting for your answer. |
| Paused or blocked, or stopped by you | The run is skipped, and the skip is recorded. |

If Theseus was closed or the machine was off while runs were due, the duty runs **once** when it can, saying how many
runs it missed. Missed runs never arrive in a burst.

Each run is told which run it is, when the last one ran and how it went, and what Theseus recorded for the card since
then, such as a spec accepted or a report confirmed. When nothing has changed, the session says "nothing new" and ends
its turn.

## Seeing and changing duties

- A card with duties shows them on its face, for example `↻ Re-triage · every 2 h · next 23:30 · last 21:30 ✓`.
- **Tools > Duties…** lists every duty on the board. **Duties…** on a card's menu lists that card's, and on a project
  card's menu, that project's.
- Tick duties, then choose **Run now**, **Pause**, **Resume**, **Edit…** or **Delete…**. **Add…** sets a new one. Run
  now does not move the duty's cadence.
- The duty dialog has three tabs: **Duty** (card, name, instruction and cadence), **Limits** (when it ends, its daily
  cap, and whether it runs through quiet hours) and **History** (its last runs and skips).

Every run, wait and skip is kept in the card's History and in the Theseus log.

## Duties set by sessions

A session sets a duty with `set_duty` instead of running a loop. It may set one on its own card, and on the cards it
may start: in a project, any card of the project; on the ordinary board, the cards made from its own proposals. It can
list duties with `duties`, and pause or end the ones sessions set with `pause_duty` and `end_duty`. A duty you set is
yours: no session can change, pause or end it.

In a project, the project file shows each duty as a `**Duty:**` line under its card's objective. Theseus writes these
lines; nobody edits them by hand.

## Limits

Set in **Tools > Options > Board**:

- **Daily cap**: the most runs a duty gives in a day, unless it sets its own. The default is 24.
- **Pauses itself**: a duty whose runs crash, or do not reach the card, this many times in a row pauses itself and says
  why on its card. The default is 3.
- **Quiet hours**: off by default. When on, duties due in those hours wait and run once when the hours end, unless the
  duty runs through them.

## The overnight queue

A session that finishes a step late in the evening often goes idle with more of its work to do, and nothing wakes it
until morning. The overnight queue is a duty that Theseus runs for each project: it tells an **open, idle** card to carry
on when all of these hold:

- the project is running;
- the card's piece of work is not done;
- nothing about it waits for you, and it is not held at a gate;
- the card is not paused or blocked, you did not stop it, and it is not a verifier;
- it has been idle longer than the threshold (30 minutes by default).

The queue never starts a card that is not running, and once it has woken a card, it does not wake it again until the
card's work has moved on. It is **off** for every project until you switch it on: tick **Resume idle cards** in the
project's **Project properties** (right-click the project's card on the board and choose **Project properties**,
or select it and press **Alt+Enter**), on the **Lead and branches** tab. Its idle threshold and its most resumes a day for
each card are in **Options > Board**.
