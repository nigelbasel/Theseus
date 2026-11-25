using Theseus.Core.Running;

namespace Theseus.Tests.Running;

[TestFixture]
public class RunStreamParserTests
{
    [Test]
    public void TheInitLine_GivesTheSessionId()
    {
        Assert.That(RunStreamParser.Parse(StreamLines.Init), Is.EqualTo(new RunInitEvent("a3263659-ae89-41c5-a7ee-71702c797ee8")));
    }

    [Test]
    public void AnAssistantLine_GivesItsTextAndTokens()
    {
        var parsed = (RunAssistantEvent)RunStreamParser.Parse(StreamLines.Text("m1", "ok", output: 4, input: 2, creation: 9, read: 7))!;

        Assert.That(parsed.Line, Is.EqualTo("ok"));
        Assert.That(parsed.MessageId, Is.EqualTo("m1"));
        Assert.That(parsed.Usage, Is.EqualTo(new RunTokens(2, 4, 9, 7)));
    }

    [Test]
    public void AToolCall_IsShownAsItsNameAndCommand()
    {
        var parsed = (RunAssistantEvent)RunStreamParser.Parse(StreamLines.Tool("m2", "git status"))!;

        Assert.That(parsed.Line, Is.EqualTo("Bash: git status"));
    }

    [Test]
    public void AResultLine_GivesItsCostTokensAndSubtype()
    {
        var parsed = (RunResultEvent)RunStreamParser.Parse(StreamLines.Result(0.2056221m))!;

        Assert.That(parsed.TotalCostUsd, Is.EqualTo(0.2056221m));
        Assert.That(parsed.Usage.CacheCreation, Is.EqualTo(50959));
        Assert.That(parsed.Subtype, Is.EqualTo("success"));
        Assert.That(parsed.HitCostCap, Is.False);
    }

    [Test]
    public void ClaudeCodesOwnCostCap_IsRecognised()
    {
        Assert.That(((RunResultEvent)RunStreamParser.Parse(StreamLines.BudgetResult)!).HitCostCap, Is.True);
    }

    [TestCase(StreamLines.Hook)]
    [TestCase("not json")]
    [TestCase("{\"type\":")]
    [TestCase("")]
    public void OtherLines_AreIgnored(string line)
    {
        Assert.That(RunStreamParser.Parse(line), Is.Null);
    }

    [Test]
    public void AUserMessage_IsOneStreamJsonLine()
    {
        Assert.That(RunStreamParser.UserMessage("Stop \"now\"\nplease"),
            Is.EqualTo("""{"type":"user","message":{"role":"user","content":"Stop \u0022now\u0022\nplease"}}"""));
    }

    [Test]
    public void TheInterrupt_IsAControlRequest()
    {
        Assert.That(RunStreamParser.Interrupt("r1"),
            Is.EqualTo("""{"type":"control_request","request_id":"r1","request":{"subtype":"interrupt"}}"""));
    }
}
