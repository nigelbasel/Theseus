using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>The evidence result block: JSON in a fenced <c>theseus-evidence</c> block, the last one current (method-v0.2).</summary>
[TestFixture]
public class EvidenceBlockReaderTests
{
    [Test]
    public void AFileWithNoBlock_HasNone()
    {
        Assert.That(EvidenceBlockReader.Last("# Evidence\n\n```json\n{ \"result\": \"pass\" }\n```\n"), Is.Null);
        Assert.That(EvidenceBlockReader.Last(null), Is.Null);
    }

    [Test]
    public void TheProposalsExampleBlock_IsRead()
    {
        const string text = "Prose first.\r\n\r\n```theseus-evidence\r\n{\r\n  \"kit\": \"method-v0.2\",\r\n  \"pipeline\": \"feature\",\r\n"
                            + "  \"stage\": \"verify\",\r\n  \"result\": \"pass\",\r\n  \"repos\": [ { \"repo\": \"Theseus\", \"build\": \"pass\" } ],\r\n"
                            + "  \"scenarios\": [ { \"name\": \"stop-with-handoff\", \"result\": \"not-run\", \"by\": null } ],\r\n"
                            + "  \"gates\": [ { \"gate\": \"spec\", \"accepted\": \"2026-11-19T21:30:00Z\" }, { \"gate\": \"plan\", \"accepted\": null } ]\r\n}\r\n```\r\n";

        var block = EvidenceBlockReader.Last(text)!;

        Assert.That(block.Passes, Is.True);
        Assert.That(block.Gates, Is.EqualTo(new[]
        {
            new EvidenceGateClaim("spec", "2026-11-19T21:30:00Z"),
            new EvidenceGateClaim("plan", null),
        }));
    }

    [Test]
    public void TheLastBlock_IsTheCurrentOne()
    {
        const string text = "```theseus-evidence\n{ \"result\": \"pass\" }\n```\n\nPart 2:\n\n```theseus-evidence\n{ \"result\": \"fail\" }\n```\n";

        Assert.That(EvidenceBlockReader.Last(text)!.Result, Is.EqualTo("fail"));
    }

    [Test]
    public void ABlockWithNoResult_SaysSo()
    {
        Assert.That(EvidenceBlockReader.Last("```theseus-evidence\n{ \"stage\": \"verify\" }\n```\n")!.Problem,
            Is.EqualTo("the last theseus-evidence block has no result"));
    }

    [Test]
    public void ABlockThatIsNotAnObject_SaysSo()
    {
        Assert.That(EvidenceBlockReader.Last("```theseus-evidence\n[ \"pass\" ]\n```\n")!.Problem,
            Is.EqualTo("the last theseus-evidence block is not a JSON object"));
    }
}
