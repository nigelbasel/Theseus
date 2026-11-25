namespace Theseus.Core.Running;

/// <summary>Every ended headless run, which the budgets and the breakers read.</summary>
public interface IRunLedger
{
    /// <summary>Adds one ended run.</summary>
    void Append(RunRecord record);

    /// <summary>Every run recorded, oldest first.</summary>
    IReadOnlyList<RunRecord> Read();
}
