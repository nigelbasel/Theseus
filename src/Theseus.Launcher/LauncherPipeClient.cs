using System.IO.Pipes;
using System.Text;
using Theseus.Core.Diagnostics;
using Theseus.Core.Launching;
using Theseus.Core.Messaging;

namespace Theseus.Launcher;

/// <summary>
/// The shim's end of the launch pipe. It redeems a ticket for a launch specification, then reports
/// the session's progress back. Reporting is best-effort: if the application has gone away the
/// session in this tab carries on regardless, because the registry, not this pipe, is the contract.
/// </summary>
public sealed class LauncherPipeClient : ILaunchReporter, IDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private LauncherPipeClient(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _reader = new StreamReader(pipe, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        _writer = new StreamWriter(pipe, encoding, leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>Connects to the named launch pipe on this machine.</summary>
    public static LauncherPipeClient Connect(string pipeName, TimeSpan timeout)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        pipe.Connect((int)timeout.TotalMilliseconds);

        return new LauncherPipeClient(pipe);
    }

    /// <summary>Presents a ticket and returns the launch specification it redeems, or null.</summary>
    public LaunchSpec? Redeem(string ticket, int shimProcessId)
    {
        Send(ShimMessageKind.Hello, new HelloPayload(ticket, shimProcessId));

        while (true)
        {
            var line = _reader.ReadLine();
            if (line is null)
            {
                return null;
            }

            if (!ShimProtocol.TryParse(line, out var envelope))
            {
                continue;
            }

            switch (envelope.Kind)
            {
                case ShimMessageKind.LaunchSpec:
                    return ShimProtocol.PayloadAs<LaunchSpec>(envelope);
                case ShimMessageKind.Error:
                    Console.Error.WriteLine(ShimProtocol.PayloadAs<ErrorPayload>(envelope)?.Message
                        ?? "The launch ticket was refused.");
                    return null;
            }
        }
    }

    /// <inheritdoc />
    public void Started(string sessionId, int claudeProcessId) =>
        Send(ShimMessageKind.Started, new StartedPayload(sessionId, claudeProcessId));

    /// <inheritdoc />
    public void Reminted(string previousSessionId, string newSessionId, string reason) =>
        Send(ShimMessageKind.Reminted, new RemintedPayload(previousSessionId, newSessionId, reason));

    /// <inheritdoc />
    public void Exited(string sessionId, int exitCode, long durationMs) =>
        Send(ShimMessageKind.Exited, new ExitedPayload(sessionId, exitCode, durationMs));

    /// <inheritdoc />
    public void Dispose()
    {
        _reader.Dispose();
        _writer.Dispose();
        _pipe.Dispose();
    }

    private void Send<TPayload>(ShimMessageKind kind, TPayload payload)
    {
        try
        {
            _writer.WriteLine(ShimProtocol.Serialise(kind, payload));
        }
        catch (IOException exception)
        {
            TheseusLog.Debug("Could not report {Kind} to Theseus: {Reason}", kind, exception.Message);
        }
        catch (ObjectDisposedException)
        {
            TheseusLog.Debug("Could not report {Kind}: the launch pipe is already closed", kind);
        }
    }
}
