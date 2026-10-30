using System.Collections.ObjectModel;

namespace Theseus.App.ViewModels;

/// <summary>
/// The board's recent notices, newest first. The status bar shows one sentence at a time, and a
/// notice replaced a second later by another was otherwise gone without being read.
/// </summary>
public sealed class NoticeLog
{
    /// <summary>How many notices are kept.</summary>
    public const int Capacity = 30;

    private readonly Func<DateTimeOffset> _clock;

    /// <summary>Creates the log over a clock.</summary>
    public NoticeLog(Func<DateTimeOffset>? clock = null) => _clock = clock ?? (() => DateTimeOffset.Now);

    /// <summary>The notices, newest first.</summary>
    public ObservableCollection<NoticeEntry> Entries { get; } = [];

    /// <summary>Records a notice. A blank one, or the same sentence again, adds nothing.</summary>
    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || (Entries.Count > 0 && Entries[0].Text == text))
        {
            return;
        }

        Entries.Insert(0, new NoticeEntry(_clock(), text));

        while (Entries.Count > Capacity)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }
}
