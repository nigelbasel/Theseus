using Theseus.Core.Mcp;

namespace Theseus.Tests.Mcp;

/// <summary>
/// Closing, archiving and reopening a project are the owner's alone (archive-a-project, brief item 4): no tool a session
/// can call does any of them. Every tool the server lists is in <see cref="TheseusMcp.ToolNames"/> (McpDutyToolsTests).
/// </summary>
[TestFixture]
public class McpNoProjectCloseToolTests
{
    [Test]
    public void NoToolASessionCanCall_ClosesArchivesOrReopensAProject() =>
        Assert.That(TheseusMcp.ToolNames.Where(tool => tool.Contains("close", StringComparison.OrdinalIgnoreCase)
                                                       || tool.Contains("archive", StringComparison.OrdinalIgnoreCase)
                                                       || tool.Contains("reopen", StringComparison.OrdinalIgnoreCase)
                                                       || tool.Contains("project", StringComparison.OrdinalIgnoreCase)),
            Is.Empty);
}
