using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class ConsoleKeystrokesTests
{
    [Test]
    public void ALine_ClearsWhatIsHalfTyped_ThenIsEachCharacterDownAndUp_ThenEnter()
    {
        var keys = ConsoleKeystrokes.ForLine("/x");

        Assert.That(keys, Is.EqualTo(new[]
        {
            new ConsoleKeystroke(ConsoleKeystrokes.ClearLine, ConsoleKeystrokes.U, true, WithControl: true),
            new ConsoleKeystroke(ConsoleKeystrokes.ClearLine, ConsoleKeystrokes.U, false, WithControl: true),
            new ConsoleKeystroke('/', 0, true),
            new ConsoleKeystroke('/', 0, false),
            new ConsoleKeystroke('x', 0, true),
            new ConsoleKeystroke('x', 0, false),
            new ConsoleKeystroke('\r', ConsoleKeystrokes.Return, true),
            new ConsoleKeystroke('\r', ConsoleKeystrokes.Return, false),
        }));
    }

    [TestCase("one\ntwo")]
    [TestCase("one\rtwo")]
    public void ALineWithABreakInIt_IsRefused_SinceTheBreakWouldSubmitPartOfIt(string line)
    {
        Assert.That(() => ConsoleKeystrokes.ForLine(line), Throws.ArgumentException);
    }
}
