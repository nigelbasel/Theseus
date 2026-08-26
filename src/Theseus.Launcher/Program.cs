using System.Diagnostics;
using Theseus.Core.Abstractions;
using Theseus.Core.Launching;
using Theseus.Launcher;

var options = LauncherOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: Theseus.Launcher --pipe <name> --ticket <id>");
    return 2;
}

using var client = LauncherPipeClient.Connect(options.PipeName, TimeSpan.FromSeconds(30));

var spec = client.Redeem(options.Ticket, Environment.ProcessId);
if (spec is null)
{
    Hold("Theseus could not supply a launch specification for this tab.");
    return 3;
}

var runner = new ClaudeProcessRunner(new ProcessLauncher(), new SessionIdFactory(), new SystemClock());
var result = runner.Run(spec, client);

foreach (var line in ExitBanner.Build(result))
{
    Console.WriteLine(line);
}

if (result.HoldTab)
{
    Console.ReadLine();
}

return result.ExitCode;

static void Hold(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine("This tab is being held open. Press Enter to close it.");
    Console.ReadLine();
}
