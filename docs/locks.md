# Locks

[Help home](index.md)

Many sessions share one machine. Some jobs cannot safely run twice at once, so Theseus holds a lock
for each and grants it to one session at a time.

| Lock | Protects |
|---|---|
| **tests** | Container-backed test suites and whole-solution builds. Two at once starve each other and fail in ways that look like real defects. |
| **dev-stack** | The shared local docker stack. |
| **llama** | The local model server on port 8090. |

## How sessions take a lock

- Test runs go through `~/.claude/bin/ndc-test-lock.sh`, which asks Theseus for the **tests** lock and
  releases it when the run ends, however it ends.
- For **dev-stack** and **llama**, a session calls `lock_acquire` before using it and `lock_release`
  when done.

If a lock is busy, the session joins a queue and waits its turn. A session never asks the holder to
give way, and never stops another session's process.

## The Locks panel

The lock button on the toolbar opens the Locks panel. It shows who holds each lock, for how long, and
who is waiting, in order. Only you reorder the queue, cancel a request or release a lock, from this
panel.

The toolbar count turns amber when a lock has been held for over 90 minutes, or when a test container
is running that no session owns. The status bar shows each lock in use and how long it has been held.

If a session holding a lock dies without releasing it, Theseus ends its test hosts, removes the test
containers it started, and grants the lock to the next in line.

If Theseus is not running, no lock is granted, and sessions wait.
