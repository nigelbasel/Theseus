namespace Theseus.Core.Terminal;

/// <summary>
/// What is written into a session's console to type a line into it: Ctrl+U, which clears anything half typed in the
/// prompt so it is not sent with the line, then each character down and up, then Enter. Writing to the console's own
/// input buffer reaches that session alone, whichever window has focus.
/// </summary>
public static class ConsoleKeystrokes
{
    /// <summary>The virtual key code for Enter.</summary>
    public const ushort Return = 0x0D;

    /// <summary>The virtual key code for U, which with Ctrl clears the line being typed.</summary>
    public const ushort U = 0x55;

    /// <summary>The character Ctrl+U produces.</summary>
    public const char ClearLine = (char)0x15;

    /// <summary>The key events that type <paramref name="line"/> and submit it.</summary>
    public static IReadOnlyList<ConsoleKeystroke> ForLine(string line)
    {
        ArgumentException.ThrowIfNullOrEmpty(line);

        if (line.Contains('\r') || line.Contains('\n'))
        {
            throw new ArgumentException("Only one line is typed: a line break would submit part of it.", nameof(line));
        }

        var keys = new List<ConsoleKeystroke>((line.Length + 2) * 2)
        {
            new(ClearLine, U, true, WithControl: true),
            new(ClearLine, U, false, WithControl: true),
        };

        foreach (var character in line)
        {
            keys.Add(new ConsoleKeystroke(character, 0, true));
            keys.Add(new ConsoleKeystroke(character, 0, false));
        }

        keys.Add(new ConsoleKeystroke('\r', Return, true));
        keys.Add(new ConsoleKeystroke('\r', Return, false));

        return keys;
    }
}
