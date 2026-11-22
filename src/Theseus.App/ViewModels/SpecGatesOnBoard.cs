using Theseus.Core.Mcp;

namespace Theseus.App.ViewModels;

/// <summary>The board's answers to gates() and accept_spec (1.1e AC 28), over the shell's own.</summary>
public sealed class SpecGatesOnBoard : ISpecGates
{
    private readonly Func<string, string> _gates;
    private readonly Func<string, string, string, string, string?> _report;

    /// <summary>Creates the answers over the shell's methods.</summary>
    public SpecGatesOnBoard(Func<string, string> gates, Func<string, string, string, string, string?> report)
    {
        _gates = gates;
        _report = report;
    }

    /// <inheritdoc />
    public string Gates(string callerKey) => _gates(callerKey);

    /// <inheritdoc />
    public string? ReportAcceptance(string callerKey, string slug, string version, string ownerWords) =>
        _report(callerKey, slug, version, ownerWords);
}
