using System.Diagnostics;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

/// <summary>
/// Typing into a real console: one made with no window, so nothing comes forward, focus stays where it is, and no
/// terminal is asked to host it.
/// </summary>
[TestFixture]
public class ConsoleTypingTests
{
    [Test]
    public void ALineTypedIntoAConsole_IsReadByTheProcessWaitingInIt()
    {
        var output = Path.Combine(Path.GetTempPath(), $"theseus-console-typing-{Guid.NewGuid():N}.txt");
        using var reader = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c \"set /p line=<CON& call echo %line%>\"{output}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;

        try
        {
            var outcome = TypeOnceItsConsoleIsUp(reader.Id, "typed into its console");

            Assert.That(reader.WaitForExit(TimeSpan.FromSeconds(20)), Is.True, "the waiting process read the line and ended");
            // cmd keeps the clear-line key as a character where Claude Code's prompt acts on it, so every key arrives, in order.
            Assert.That((outcome, File.ReadAllText(output).Trim()),
                Is.EqualTo((ConsoleTypingOutcome.Sent, ConsoleKeystrokes.ClearLine + "typed into its console")));
        }
        finally
        {
            if (!reader.HasExited)
            {
                reader.Kill();
            }

            File.Delete(output);
        }
    }

    [Test]
    public void AProcessThatIsGone_IsNotTypedInto()
    {
        using var ended = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c exit") { UseShellExecute = false, CreateNoWindow = true })!;
        ended.WaitForExit();

        Assert.That(ConsoleTyping.TypeLine(ended.Id, "nothing"), Is.EqualTo(ConsoleTypingOutcome.NotAttached));
    }

    // A console is made a moment after its process starts; joining it before then fails.
    private static ConsoleTypingOutcome TypeOnceItsConsoleIsUp(int processId, string line)
    {
        var outcome = ConsoleTypingOutcome.NotAttached;

        for (var attempt = 0; attempt < 50 && outcome == ConsoleTypingOutcome.NotAttached; attempt++)
        {
            Thread.Sleep(100);
            outcome = ConsoleTyping.TypeLine(processId, line);
        }

        return outcome;
    }
}
