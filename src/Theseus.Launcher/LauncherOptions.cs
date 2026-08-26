namespace Theseus.Launcher;

/// <summary>
/// The shim's entire command line: a pipe name and a ticket, both fixed-alphabet tokens. Nothing
/// with a space, a quote or a semicolon is ever passed here, so the terminal has nothing to mangle.
/// </summary>
/// <param name="PipeName">The launch pipe to connect to.</param>
/// <param name="Ticket">The single-use ticket that redeems a launch specification.</param>
public sealed record LauncherOptions(string PipeName, string Ticket)
{
    /// <summary>Parses the shim's arguments, returning null when they are not usable.</summary>
    public static LauncherOptions? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? pipe = null;
        string? ticket = null;

        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            switch (args[i])
            {
                case "--pipe":
                    pipe = args[i + 1];
                    break;
                case "--ticket":
                    ticket = args[i + 1];
                    break;
                default:
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(pipe) || string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        return new LauncherOptions(pipe, ticket);
    }
}
