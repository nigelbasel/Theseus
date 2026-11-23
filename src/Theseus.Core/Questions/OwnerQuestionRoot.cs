namespace Theseus.Core.Questions;

/// <summary>
/// Where the questions are kept: beside the stop and tell requests, because the hook, running inside the session,
/// writes and clears them, and the board reads them, with nothing between the two but this folder.
/// </summary>
public static class OwnerQuestionRoot
{
    /// <summary>The folder both the board and the hook use.</summary>
    public static string Default { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", "owner-questions");
}
