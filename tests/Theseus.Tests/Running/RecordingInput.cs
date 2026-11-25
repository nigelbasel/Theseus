using Theseus.Core.Running;

namespace Theseus.Tests.Running;

/// <summary>A headless run's stdin that keeps what it was given.</summary>
internal sealed class RecordingInput : IHeadlessInput
{
    public List<string> Lines { get; } = [];

    public int Closes { get; private set; }

    public void WriteLine(string line) => Lines.Add(line);

    public void Close() => Closes++;
}
