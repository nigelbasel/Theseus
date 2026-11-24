using Theseus.Core.Abstractions;
using Theseus.Core.Domain;

namespace Theseus.Tests.Windows;

/// <summary>A run log with nothing in it and that keeps nothing, for a drawer built in a window test.</summary>
internal sealed class NoRunLog : IRunLog
{
    public void Append(SessionRun run)
    {
    }

    public void AppendEnd(RunEnd end)
    {
    }

    public IReadOnlyList<SessionRun> Read() => [];
}
