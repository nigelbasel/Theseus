namespace Theseus.Core.Running;

/// <summary>A headless run's stdin: one stream-json line at a time, then closed, which tells Claude Code no more input is coming.</summary>
public interface IHeadlessInput
{
    /// <summary>Writes one line and flushes it.</summary>
    void WriteLine(string line);

    /// <summary>Closes the stream. Safe to call twice.</summary>
    void Close();
}
