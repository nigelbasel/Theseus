using Theseus.Core.Abstractions;

namespace Theseus.Tests.Abstractions;

/// <summary>
/// The real launcher sets a request's variables on the child it starts: the last step of a role reaching a session's
/// hooks (verifier-card AC 2; verify part 1, surviving mutation 2).
/// </summary>
[TestFixture]
public class ProcessLauncherEnvironmentTests
{
    [Test]
    public void AVariableInTheRequest_IsSetOnTheChild()
    {
        var request = new ProcessStartRequest(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            ["/c", "exit %THESEUS_LAUNCH_PROBE%"],
            Path.GetTempPath())
        {
            Environment = new Dictionary<string, string> { ["THESEUS_LAUNCH_PROBE"] = "7" },
        };

        using var handle = new ProcessLauncher().Start(request);

        Assert.That(handle.WaitForExit(), Is.EqualTo(7), "the child read the variable and exited with it");
    }
}
