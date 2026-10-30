namespace Theseus.App.ViewModels;

/// <summary>One thing the board said, and when, for the status bar's list of earlier notices.</summary>
/// <param name="At">When it was said, local time.</param>
/// <param name="Text">What was said.</param>
public sealed record NoticeEntry(DateTimeOffset At, string Text)
{
    /// <summary>How the list shows it: the time, then the sentence.</summary>
    public string Display => $"{At:HH:mm}  {Text}";
}
