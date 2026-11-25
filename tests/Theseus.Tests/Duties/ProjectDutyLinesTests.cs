using Theseus.Core.Duties;

namespace Theseus.Tests.Duties;

/// <summary>The project file's duty lines: written under the right objective's table, rewritten whole, removed when none.</summary>
[TestFixture]
public class ProjectDutyLinesTests
{
    private const string File =
        "## Phase 2: Unattended\n\n"
        + "### Objective 2.2: A runner\n[criteria to be written]\n\n**Pipeline:** feature\n"
        + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| runner-card | runner | nigel/runner | Theseus |\n\n"
        + "### Objective 2.3: A queue\n- [ ] it runs\n\n**Pipeline:** feature\n"
        + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| queue-card | queue | nigel/queue | Theseus |\n\n"
        + "## Phase 3: The Hub\n";

    [Test]
    public void Write_PutsEachObjectivesLinesAfterItsTable()
    {
        var written = ProjectDutyLines.Write(File, new Dictionary<string, IReadOnlyList<string>>
        {
            ["2.3"] = ["**Duty:** Queue · Check · every 20 min · look"],
        });

        Assert.That(written, Does.Contain("| queue-card | queue | nigel/queue | Theseus |\n\n**Duty:** Queue · Check · every 20 min · look\n\n## Phase 3"));
        Assert.That(written, Does.Not.Contain("Theseus |\n\n**Duty:** Queue · Check · every 20 min · look\n\n### Objective 2.3"));
    }

    [Test]
    public void Write_Again_ReplacesTheLines_AndWithNoneLeavesTheFileAsItWas()
    {
        var once = ProjectDutyLines.Write(File, new Dictionary<string, IReadOnlyList<string>> { ["2.3"] = ["**Duty:** a"] });
        var twice = ProjectDutyLines.Write(once, new Dictionary<string, IReadOnlyList<string>> { ["2.3"] = ["**Duty:** b"], ["2.2"] = ["**Duty:** c"] });
        var none = ProjectDutyLines.Write(twice, new Dictionary<string, IReadOnlyList<string>>());

        Assert.Multiple(() =>
        {
            Assert.That(twice, Does.Not.Contain("**Duty:** a"));
            Assert.That(twice, Does.Contain("| runner-card | runner | nigel/runner | Theseus |\n\n**Duty:** c\n"));
            Assert.That(none, Is.EqualTo(File));
        });
    }

    [Test]
    public void Write_KeepsTheFilesLineEndings()
    {
        var crlf = File.Replace("\n", "\r\n", StringComparison.Ordinal);

        var written = ProjectDutyLines.Write(crlf, new Dictionary<string, IReadOnlyList<string>> { ["2.3"] = ["**Duty:** a"] });

        Assert.That(written.Replace("\r\n", string.Empty, StringComparison.Ordinal), Does.Not.Contain("\n"));
    }

    [Test]
    public void Line_NamesTheCardTheDutyItsCadenceAndItsInstruction()
    {
        var duty = new Duty("d-00000001", "card", "Queue runner", "keep the queue running\nmore", DutyCadence.Every(TimeSpan.FromMinutes(20)),
            DutyEnd.Never, null, DateTimeOffset.UnixEpoch, new DutyRunState(DateTimeOffset.UnixEpoch));

        Assert.That(ProjectDutyLines.Line(duty, "NDC bug triage"),
            Is.EqualTo("**Duty:** NDC bug triage · Queue runner · every 20 min · keep the queue running"));
    }
}
