namespace Theseus.Core.Help;

/// <summary>
/// What the board says about itself.
///
/// Starting and stopping a session both run through several parts - a terminal, a launcher, a named
/// pipe, a request file, a hook inside somebody else's process - and when one of them does not do
/// its job the board shows nothing at all. The failure is silent, and silence is unreadable: there
/// is no way to tell "the request was never written" from "the session has not finished its turn"
/// from "the hook is not installed" by looking. So the ways it fails are written down beside the way
/// it works, in the application rather than in a document nobody has open.
/// </summary>
public static class HelpBook
{
    /// <summary>Every section, in the order they are worth reading.</summary>
    public static IReadOnlyList<HelpSection> Sections { get; } =
    [
        new HelpSection(
            "Starting a session",
            "Start opens a Windows Terminal tab, runs the launcher in it, and hands that launcher a"
            + " ticket. The launcher collects the session's instructions over a named pipe and starts"
            + " Claude Code in the working directory. The session identifier is minted here, before"
            + " the tab exists, which is what makes the session resumable tomorrow.",
            [
                new HelpEntry(
                    "First run and continuing",
                    "A workstream that has run before is not handed its original prompt again - that"
                    + " prompt describes starting the work, and replaying it invites the session to"
                    + " redo what is already done. It is given a continuation pointing at its"
                    + " handover notes instead."),
                new HelpEntry(
                    "Every session is told who it is",
                    "The opening lines name the workstream, name its handover document, and ask that"
                    + " the document be brought up to date whenever the session is told to stop or"
                    + " wrap up. That is why stopping a session by typing into its tab works."),
                new HelpEntry(
                    "\"That launch ticket is unknown or has already been used\"",
                    "The tab found a different copy of Theseus than the one that opened it, or the"
                    + " ticket expired before the tab got going - it is good for two minutes. Almost"
                    + " always it means two copies of Theseus were running. Close both and start again."),
                new HelpEntry(
                    "The tab opens in the wrong terminal",
                    "Theseus prefers a Windows Terminal that is already running, and prefers the"
                    + " stable build over Preview when nothing is. Set the path explicitly in"
                    + " Settings to end the argument."),
                new HelpEntry(
                    "Start does nothing at all",
                    "Claude Code or Windows Terminal could not be found - the status strip says which"
                    + " - or the card is already following a live session. Both have explicit paths"
                    + " in Settings."),
            ]),

        new HelpSection(
            "Stopping a session",
            "Stop writes a request naming the session and the file it should write. A hook registered"
            + " in your Claude Code settings runs at the end of every turn in every session on this"
            + " machine; when it finds a request waiting for the session it is running in, it refuses"
            + " that session's ending and hands it the request. The session writes its handover notes"
            + " and stops. A request is claimed exactly once, so no session is asked twice.",
            [
                new HelpEntry(
                    "You can always do it yourself",
                    "Type \"stop and hand over\" into the tab. There is nothing to paste: the session"
                    + " was told its handover file when it started, and asked to keep it. The button"
                    + " saves the typing; it is not the mechanism."),
                new HelpEntry(
                    "Nothing happens, and the hook is not registered",
                    "Then nothing was delivered and nothing will be. The hook lives in your own Claude"
                    + " Code settings, which Theseus reads and never writes - it governs what runs"
                    + " inside every session on the machine, and an installer editing it quietly is"
                    + " not something you could reason about. Run install.ps1: it prints the exact"
                    + " configuration to merge."),
                new HelpEntry(
                    "Nothing happens, and the hook is registered",
                    "Hooks are loaded when a session starts, so a session older than the registration"
                    + " does not have one. Restart that session and it will. This is the usual answer."),
                new HelpEntry(
                    "The session is sitting idle",
                    "The request is delivered at the end of a turn, and a session waiting for input is"
                    + " not going to have another one. Theseus brings such a tab to the front so you"
                    + " can press Enter; that ends a turn, and the request lands."),
                new HelpEntry(
                    "The terminal is running as administrator",
                    "Windows will not let Theseus type into a window belonging to a more privileged"
                    + " process, so a tab that needs nudging cannot be nudged. Stop that one by hand,"
                    + " or run its terminal unelevated."),
                new HelpEntry(
                    "Requests expire after four hours",
                    "Long enough to survive a session deep in a slow turn, short enough that a request"
                    + " made this evening cannot interrupt tomorrow morning. An expired one is dropped"
                    + " rather than delivered late, and swept away on the next refresh."),
            ]),

        new HelpSection(
            "The other buttons",
            "Everything else on a card acts on the session that card is following.",
            [
                new HelpEntry("Start", "Opens a tab for this workstream."),
                new HelpEntry("Stop", "Asks this session to write its handover notes and exit when its turn ends."),
                new HelpEntry("Focus", "Brings this session's tab to the front. The taskbar icon's menu does this too, without the board."),
                new HelpEntry(
                    "Adopt",
                    "Makes a card follow a session again after that session was restarted outside"
                    + " Theseus. Until it does, the card is looking for a session identifier nothing"
                    + " answers to, so Stop and Focus have nothing to act on."),
                new HelpEntry("Start lane", "Starts every workstream in the lane that is not already running."),
                new HelpEntry("Start board", "Starts every lane except the last, which is where finished work lives."),
                new HelpEntry("Stop all", "Asks every running session to hand over, one request each."),
                new HelpEntry("Refresh", "Re-reads the plans, the run log and the session registry. Nothing is cached that this will not correct."),
            ]),

        new HelpSection(
            "What a card is telling you",
            "A card's colour and wording follow the session, not the plan file.",
            [
                new HelpEntry("Running, idle, needs attention", "Read from the live session registry, and animated while a session is working or waiting."),
                new HelpEntry(
                    "Ended",
                    "The session is gone but ran today. A session that finishes is not a workstream"
                    + " that was never started, and the board is a record of the day."),
                new HelpEntry(
                    "Not started today",
                    "No run of this workstream was under way at any point today. A session left"
                    + " working overnight counts as today's, whatever day it was started on."),
                new HelpEntry(
                    "Restarted outside Theseus",
                    "A session is running in this workstream's working directory that the board is not"
                    + " following. Adopt it."),
            ]),
    ];
}
