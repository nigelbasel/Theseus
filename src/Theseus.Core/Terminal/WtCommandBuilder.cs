namespace Theseus.Core.Terminal;

/// <summary>
/// Builds the argument vector that opens a tab in Windows Terminal. The result is passed through
/// <c>ProcessStartInfo.ArgumentList</c> so the runtime handles quoting; joining it into a single
/// string reintroduces the quoting faults this type exists to avoid.
/// </summary>
public static class WtCommandBuilder
{
    /// <summary>Builds the argument vector for a new tab.</summary>
    public static IReadOnlyList<string> Build(TerminalTabRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var argv = new List<string>
        {
            "-w",
            WtArgumentEscaper.Escape(request.WindowName),
            "new-tab",
            "--title",
            WtArgumentEscaper.Escape(request.Title),
            "--tabColor",
            request.ColourHex,
            "--suppressApplicationTitle",
            "-d",
            WtArgumentEscaper.Escape(request.WorkingDirectory),
            "--",
            WtArgumentEscaper.Escape(request.TargetExecutable),
        };

        argv.AddRange(request.TargetArguments.Select(WtArgumentEscaper.Escape));

        return argv;
    }
}
