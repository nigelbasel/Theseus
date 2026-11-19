# close-step: evidence

Pipeline: `feature` (method kit v0, P1 to P18), stage **verify**. Part 1 is in `evidence-part1-attempt1.md` (FAIL) and
`evidence-part1-attempt2.md` (PASS). This file is part 2, the live check.

**Verify as a whole stays open:** the holdout `stop-with-handoff` has not been run. Under P18 and Nigel's decision of
2026-11-19 it waits for the verifier card (phase 1 objective 1.0), and pull request #1
(https://github.com/nigelbasel/Theseus/pull/1) stays unmerged until then.

## Part 2: live check (2026-11-19, Nigel at the board)

Board **2026.11.19.29130**, built by the pull request's CI run
(https://github.com/nigelbasel/Theseus/actions/runs/44562671864, build green, release skipped as designed), installed by
Nigel from the run's `theseus-installer` artifact, started 16:17:11. Scratch card "Stop check: stays idle" (session
`d616ea10`). Times are from the board's log and the session's transcript. Steps as written in
`evidence-part1-attempt2.md`, checked against the code.

| Step | What happened | Result |
|---|---|---|
| 1. A card with no stop (AC 7) | Started at 16:18:16. Nigel: "stop icon showing, no amber icon" | **Pass** |
| 2. Stop and touch nothing (AC 1, 5, 6) | Stop at 16:20:49.9, nudged (Sent), handed over after 0.7 s, handoff 7.4 s later. Nigel: the icon turned amber and the tooltip showed | **Pass** |
| 3. The close time (AC 1, 12) | The session ended its turn at **16:21:03.1** (transcript `stop_hook_summary`). `/exit` typed at **16:21:24.4**, "Sent", with no other board activity between: **21.3 s after going quiet** (20 s quiet + 1 s margin). The log has "Stop of stop-check-stays-idle finished in 34.5s: delivered after 0.7s, handoff 7.4s later, /exit 26.4s after that" and the unchanged "Closing ... : Sent" line | **Pass.** Before this change: 87 s, 112 s and 11 minutes |
| 4. Between `/exit` and exit (AC 5, 6; F1) | The closing moment is about a second long. Nigel saw the amber icon and its tooltip during the stop, but did not single out the "Typed /exit ... Closing." text | **Not observed live**; rests on `StopViewTests` |
| 5. The next run is not shown closing (F1) | Started again at 16:23:48. Nigel: "stop icon showing, no amber icon" | **Pass** |
| 6. Second press (AC 10, in part) | A further run started 16:28:13; Stop at 16:29:21.7, then Stop selected at **16:29:28.1**: "Did not ask stop-check-stays-idle again: it has already been handed its stop". Nigel found the notice in the status bar | **Pass** |
| 7. A held-back stop (F7) | Optional, not run | Rests on `StopViewTests` |

Also in the log: a stop at 16:23:57.9 on the step 5 run closed the same way (handoff 21.7 s after delivery, `/exit`
26.5 s after the handoff, "Sent"). Every close today came 26.3 s to 26.5 s after the handoff was written.

**An error in the steps as given to Nigel:** step 6 was described to him as a "banner"; the notice is in the status
bar's left end (`MainWindow.xaml:1845-1876`). The step was given from memory, not checked against the code (P4); the
behaviour itself was right.

**Verdict, part 2: passes** on everything it can reach. Not reachable live, as planned: V1 (AC 8), every failed
`/exit` path and Close now (AC 9), and the AC 10 failure notice; step 4's closing text and step 7 were not observed.
