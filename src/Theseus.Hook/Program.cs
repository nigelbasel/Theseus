using System.Text;
using Theseus.Core.Status;
using Theseus.Core.Stopping;

// This runs on every turn of every Claude Code session on the machine, including sessions that have
// nothing to do with Theseus. It must be quick, and it must be silent about everything except a
// request genuinely waiting: a session should never learn from its own hooks that Theseus is
// installed, let alone installed badly. Every failure is therefore swallowed and reported as
// nothing to say. Delivering is the one case that speaks, and on a Stop event speaking means exiting
// 2: the session is refused its ending, and reads standard error as what to do instead.
try
{
    // Decode the bytes as UTF-8 and drop any preamble, rather than taking Console.In, whose decoding
    // follows the console's code page: a byte-order mark arrives as three mojibake characters that
    // way, which no amount of trimming for the one character it should have been will remove.
    using var standardInput = Console.OpenStandardInput();
    using var received = new MemoryStream();

    standardInput.CopyTo(received);

    var bytes = new ReadOnlySpan<byte>(received.GetBuffer(), 0, (int)received.Length);
    var preamble = Encoding.UTF8.Preamble;

    if (bytes.StartsWith(preamble))
    {
        bytes = bytes[preamble.Length..];
    }

    var text = Encoding.UTF8.GetString(bytes);
    var input = StopHookInput.Parse(text);

    if (input?.SessionId is not { } sessionId)
    {
        return 0;
    }

    // Registered only for the sessions Theseus launches, and only on its own tools: a call made while
    // Theseus is down is answered with why, rather than failing to connect with nothing said.
    if (input.HookEventName == "PreToolUse")
    {
        if (StatusHook.PreToolUseReply(StatusHook.ToolName(text), TheseusStatusFile.Read(), TheseusStatusFile.IsRunning,
                TimeZoneInfo.Local) is { } refusal)
        {
            Console.Out.Write(refusal);
        }

        return 0;
    }

    // Decide before claiming. Claiming is what makes a request undeliverable to anyone else, so
    // claiming first and then declining - on a subagent's turn, or one a stop hook already extended
    // - would consume the request and leave the session that was meant to have it none the wiser.
    var store = new StopRequestStore(StopRequestRoot.Default);
    var waiting = store.Waiting(sessionId);

    if (StopHookDecision.Deliver(input, waiting, DateTimeOffset.UtcNow) is null
        || store.Claim(sessionId) is not { } request)
    {
        // Nothing to stop for. A session Theseus launched is told, once, if Theseus has stopped or
        // restarted since it last looked: it cannot be told by Theseus itself once Theseus is gone.
        if (input.HookEventName == "UserPromptSubmit"
            && StatusHook.PromptReply(sessionId, new StatusSeenStore(), TheseusStatusFile.Read(), TheseusStatusFile.IsRunning,
                TimeZoneInfo.Local) is { } notice)
        {
            Console.Out.Write(notice);
        }

        return 0;
    }

    var delivery = StopHookDelivery.For(input.HookEventName, request.Text);

    Console.Out.Write(delivery.StandardOutput);
    Console.Error.Write(delivery.StandardError);

    return delivery.ExitCode;
}
catch (Exception)
{
    return 0;
}
