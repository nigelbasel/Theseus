using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>
/// Tells a session which workstream it is.
///
/// A session was given a standing preamble and a prompt and nothing else: not its own name, not
/// which notes on disk were its own, not that other workstreams keep theirs in the same places. It
/// had no way to know, so an ambient instruction like "read any handoff files and continue" was
/// answered with whatever lay nearest — and two sessions ended up revising one document, which they
/// only avoided ruining because one of them noticed the file moving and asked.
///
/// Scope cannot be inferred from a working directory several workstreams share. It has to be said.
/// </summary>
public static class WorkstreamIdentity
{
    /// <summary>The lines that open every session, before anything else it is told.</summary>
    public static string For(Workstream workstream, string handoffPath)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var title = string.IsNullOrWhiteSpace(workstream.Title) ? workstream.Key : workstream.Title.Trim();

        return string.Join(Environment.NewLine,
            $"You are the \"{title}\" workstream ({workstream.Key}), one of several on this board.",
            $"Your handover notes are {handoffPath}. Whenever you are asked to stop, wrap up, hand"
            + " over or leave things for the next session - however it is worded, and whether or not"
            + " the file is named - bring that document up to date and write it there, rather than"
            + " starting notes of your own somewhere else. Say what you did, what is unfinished, and"
            + " what someone resuming should do next. It is the only thing your next session is"
            + " given, so anything not in it is lost.",
            "Other workstreams keep their own notes beside yours and their work in the same"
            + " directories. Do not take up work belonging to another one: if what you find is"
            + " theirs, say so rather than doing it.");
    }
}
