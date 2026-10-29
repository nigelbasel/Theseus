using System.Diagnostics;
using System.Runtime.InteropServices;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
[NonParallelizable]
public class ConsoleTitleReaderTests
{
    private const int StandardInput = -10;
    private const int StandardOutput = -11;
    private const int StandardError = -12;

    [Test]
    public void Read_LeavesAWindowedProcessAbleToStartChildren()
    {
        var saved = new[] { StandardInput, StandardOutput, StandardError }
            .Select(which => (which, handle: GetStdHandle(which)))
            .ToArray();

        using var console = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
        })!;

        try
        {
            FreeConsole();

            foreach (var (which, _) in saved)
            {
                SetStdHandle(which, IntPtr.Zero);
            }

            Thread.Sleep(1000);
            new ConsoleTitleReader().Read(console.Id);

            Assert.DoesNotThrow(() =>
                {
                    using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    })!;

                    child.WaitForExit();
                },
                "on .NET 11 a released console's handles stayed behind and every Start button failed, 2026-10-23");
        }
        finally
        {
            foreach (var (which, handle) in saved)
            {
                SetStdHandle(which, handle);
            }

            console.Kill(entireProcessTree: true);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int which);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetStdHandle(int which, IntPtr handle);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();
}
