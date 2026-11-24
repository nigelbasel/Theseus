using Theseus.Core.Abstractions;
using Theseus.Core.Running;

namespace Theseus.Tests.Running;

/// <summary>A headless run's process that the test drives: it says what it was started with, and writes and exits when told.</summary>
internal sealed class FakeHeadlessProcess : IHeadlessProcess
{
    private readonly Action<string> _line;
    private readonly Action<int> _exited;

    public FakeHeadlessProcess(ProcessStartRequest request, Action<string> line, Action<int> exited)
    {
        Request = request;
        _line = line;
        _exited = exited;
    }

    public ProcessStartRequest Request { get; }

    public List<string> Input { get; } = [];

    public bool Closed { get; private set; }

    public bool WasEnded { get; private set; }

    public int ProcessId => 4242;

    public string? LastError { get; set; }

    public void WriteLine(string line) => Input.Add(line);

    public void Close() => Closed = true;

    public void End() => WasEnded = true;

    public void Writes(string line) => _line(line);

    public void Exits(int code) => _exited(code);

    /// <summary>A run that begins, ends its turn with a cost, and exits cleanly.</summary>
    public void Completes(decimal cost)
    {
        Writes(StreamLines.Init);
        Writes(StreamLines.Result(cost));
        Exits(0);
    }
}
