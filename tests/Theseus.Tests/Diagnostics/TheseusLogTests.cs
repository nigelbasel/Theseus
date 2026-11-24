using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Theseus.Core.Diagnostics;

namespace Theseus.Tests.Diagnostics;

[TestFixture]
public class TheseusLogTests
{
    [Test]
    public void ALine_HasTheDateTimeLevelInFullAndSource_AsTheEstatesLogsDo()
    {
        var at = new DateTimeOffset(2026, 11, 24, 12, 25, 3, 456, TimeSpan.Zero);
        var message = new MessageTemplateParser().Parse("Cleared up launcher {Pid}");
        var line = new StringWriter();

        new MessageTemplateTextFormatter(TheseusLog.LineTemplate).Format(
            new LogEvent(at, LogEventLevel.Information, null, message, [new LogEventProperty("Pid", new ScalarValue(4242))]), line);

        Assert.That(line.ToString().TrimEnd(), Is.EqualTo("[2026-11-24 12:25:03.456] [Information] <Theseus> Cleared up launcher 4242"));
    }
}
