using System.Diagnostics;
using System.Text;
using Theseus.Core.Abstractions;

namespace Theseus.Core.Running;

/// <summary>
/// A headless run's Claude Code process, with stdin, stdout and stderr redirected: the opposite of
/// <see cref="ProcessLauncher"/>, which must not redirect, because a tab's session needs its console.
/// </summary>
public sealed class HeadlessProcess : IHeadlessProcess, IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Process _process;
    private readonly Lock _gate = new();
    private bool _closed;

    private HeadlessProcess(Process process) => _process = process;

    /// <summary>Its process id.</summary>
    public int ProcessId => _process.Id;

    /// <summary>The last line it wrote to stderr, for a crash's record.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Starts the process. <paramref name="line"/> is called for every stdout line and <paramref name="exited"/> once, with
    /// the exit code, after the last line has been read; both on thread-pool threads.
    /// </summary>
    public static HeadlessProcess Start(ProcessStartRequest request, Action<string> line, Action<int> exited)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(exited);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in request.Environment)
        {
            startInfo.Environment[name] = value;
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{request.ExecutablePath}'.");

        var owner = new HeadlessProcess(process);

        _ = Task.Run(async () =>
        {
            var errors = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } error)
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        owner.LastError = error.Trim();
                    }
                }
            });

            while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } text)
            {
                line(text);
            }

            await errors.ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            exited(process.ExitCode);
        });

        return owner;
    }

    /// <inheritdoc />
    public void WriteLine(string line)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            try
            {
                _process.StandardInput.WriteLine(line);
                _process.StandardInput.Flush();
            }
            catch (IOException)
            {
                _closed = true;
            }
        }
    }

    /// <inheritdoc />
    public void Close()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;

            try
            {
                _process.StandardInput.Close();
            }
            catch (IOException)
            {
                // The process has gone already; there is nothing left to tell it.
            }
        }
    }

    /// <summary>Ends the process. Used only for a run that never began a session, or one that would not exit after an interrupt.</summary>
    public void End()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It has exited already.
        }
    }

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();
}
