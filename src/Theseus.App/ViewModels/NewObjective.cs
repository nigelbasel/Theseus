namespace Theseus.App.ViewModels;

/// <summary>What Add objective… asks for (1.1c AC 8).</summary>
/// <param name="Title">Its title.</param>
/// <param name="Criteria">Its acceptance criteria, one each; blanks are dropped.</param>
public sealed record NewObjective(string Title, IReadOnlyList<string> Criteria);
