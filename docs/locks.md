# Locks

[Help home](index.md)

Many sessions share one machine. Some jobs cannot safely run at the same time, so Theseus holds the
machine's shared **resources** and grants each request the ones it needs, all at once or not at all.

| Resource | Capacity | Protects |
|---|---|---|
| **sql-containers** | 1 | SQL Server test containers. Two container-backed suites at once starve each other and fail in ways that look like real defects. |
| **cpu-build** | slots, 1 until measured | Whole-solution builds. |
| **docker-build** | 1 | Docker image builds and the package restore inside them. |
| **compose:&lt;project&gt;** | 1 each | One docker compose project, by name. Two projects with different names do not clash. |
| **port:&lt;n&gt;** | 1 each | A host port a stack publishes. |
| **worktree:&lt;path&gt;** | 1 each | A source tree's build output. Two builds in one tree corrupt each other. |
| **dev-stack** | 1 | The shared local docker stack. |
| **llama** | 1 | The local model server on port 8090. |

The table lives in `resources.json` in Theseus's data folder. A request may name a resource the table
does not know; it is held alone, clashing with everything, until the **arbiter** rules on it.

The old lock names still work. **tests** means everything the old single lock guarded:
`sql-containers`, every `cpu-build` slot and `docker-build`. **dev-stack** and **llama** are one
resource each.

## How sessions ask

- Builds and container suites go through `~/.claude/bin/ndc-test-lock.sh`. Theseus works out from the
  command what it needs, so a solution build holds a build slot and its source tree, and a
  persistence suite holds the SQL containers and its tree. A build in one tree can run beside a suite
  in another.
- `ndc-test-lock.sh steps "<who>" "<label>" -- <build> ::: <unit suite> ::: <container suite>` runs
  each command under its own hold and releases it after; later steps keep the sequence's place.
  Chaining commands with `&&` holds everything they need for the whole chain.
- For anything held across many commands, a session calls `lock_acquire` with a scope, such as
  `["dev-stack"]` or `["compose:members-hub", "port:8080"]`, and `lock_release` when done.

If what a request needs is busy, it joins one queue, in order. A later request may go ahead of an
earlier one only if it needs nothing the earlier one waits for, or twice at most; after that, what the
earlier one needs is held back for it. A session never asks a holder to give way, and never stops
another session's process.

**The same run twice.** When a card, or a verifier of that card, asks to run a command on a commit
that is already waiting or running, it is refused and told to wait for the first. `status` shows
how recent runs ended. `--again` asks for a second run on purpose, such as for a flaky test.

## Deadlocks

A wait cycle can only form across two requests from one session: holding one thing while a command
asks for another. Theseus checks every request that would wait. If it could never be granted,
because the sessions involved would wait on each other for ever, it is refused at once, saying who
holds what. Nothing is stopped. Each refusal is listed on the Locks panel under **Stopped deadlocks**.
Ask for everything you need in one request.

## The Locks panel

The lock button on the toolbar opens the Locks panel. It has a row for each resource: its capacity,
who holds how much of it and for how long, and who waits, in order. A request for several resources
shows on each of them. Only you reorder the queue, cancel a request or release a hold, from this
panel.

A hold taken by a session that has been idle for 30 minutes, or whose session has gone, shows amber
with how long, for you to release. Nothing releases it by itself.

**Rulings** lists what the arbiter ruled, with **Overturn**. **Not ruled on yet** lists resources
requests have named that the table does not know.

The toolbar count turns amber when a hold has lasted over 90 minutes, when a session hold is amber,
or when a test container is running that no session owns.

If a session holding SQL containers dies without releasing them, Theseus ends its test hosts,
removes the test containers it started, and grants the next in line.

If Theseus is not running, nothing is granted, and sessions wait.

## When the machine sleeps

A waiting request has to keep asking, or it loses its place. Silence is counted only in time Theseus
itself was awake: after the machine sleeps, nobody in the queue is dropped for having been asleep too.

## Options

**Advanced** in Options has the lock client, its guard and the command classes they share, with
**Install** and **Undo**, and the **Arbiter**: the card whose sessions may rule on a resource the table
does not know. With no arbiter, unknown resources stay held alone.

## When Theseus stops or restarts

Theseus writes a small status file when it starts and when it closes, because once it has stopped it
can tell sessions nothing itself. Sessions learn from that file:

- A session that calls one of Theseus's tools while Theseus is down is told why the call failed, such
  as "Theseus is not running: it was closed at 17:20" or "it stopped unexpectedly", instead of seeing
  only a connection error.
- The next time you type into a session after Theseus stopped or restarted, it is told once. After a
  restart it is told that any lock it held has gone, and to ask you to run `/mcp` if Theseus's tools
  fail to reconnect.
- The test lock script says the same when it cannot reach Theseus.
