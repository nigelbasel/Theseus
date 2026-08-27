using System.Text.Json;
using Theseus.Core.Persistence;
using Theseus.Core.Stopping;

// This runs on every turn of every Claude Code session on the machine, including sessions that have
// nothing to do with Theseus. It must be quick, and it must be silent about everything except a
// request genuinely waiting: a session should never learn from its own hooks that Theseus is
// installed, let alone installed badly. Every failure is therefore swallowed, and the exit status is
// always zero - a Stop hook that exits non-zero blocks the turn it was supposed to be extending.
try
{
    var input = JsonSerializer.Deserialize<StopHookInput>(Console.In.ReadToEnd(), TheseusJson.Compact);

    if (input?.SessionId is not { } sessionId)
    {
        return 0;
    }

    var store = new StopRequestStore(StopRequestRoot.Default);
    var waiting = store.Claim(sessionId);

    if (StopHookDecision.Deliver(input, waiting, DateTimeOffset.UtcNow) is not { } request)
    {
        return 0;
    }

    Console.Out.Write(StopHookReply.For(input.HookEventName ?? "Stop", request.Text));
}
catch (Exception)
{
    return 0;
}

return 0;
