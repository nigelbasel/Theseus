using System.Diagnostics;
using Theseus.App.Services;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Reporting;
using Theseus.Core.Stopping;

namespace Theseus.App.ViewModels;

/// <summary>
/// The day's report: what was worked on, for how long, and which tabs can be closed.
///
/// It is written to a file as well as shown, because the reason it exists is that time logged by hand
/// is unreliable — and something you can paste into a worklog is worth more than something you can
/// only read. The file is the artefact; this window is a way of looking at it without leaving the
/// board.
/// </summary>
public sealed class DayReportViewModel : ObservableObject
{
    private readonly IRunLog _runs;
    private readonly Func<IReadOnlyList<Workstream>> _workstreams;
    private readonly Func<string, bool> _isLive;
    private bool _isOpen;
    private string _markdown = string.Empty;
    private string _path = string.Empty;
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Creates the report over the run log it reads.</summary>
    public DayReportViewModel(
        IRunLog runs, Func<IReadOnlyList<Workstream>> workstreams, Func<string, bool> isLive)
    {
        _runs = runs;
        _workstreams = workstreams;
        _isLive = isLive;

        OpenCommand = new RelayCommand(_ =>
        {
            Build(DateOnly.FromDateTime(DateTime.Now));
            IsOpen = true;
        });

        CloseCommand = new RelayCommand(_ => IsOpen = false);
        RefreshCommand = new RelayCommand(_ => Build(_day));
        YesterdayCommand = new RelayCommand(_ => Build(_day.AddDays(-1)));
        TomorrowCommand = new RelayCommand(_ => Build(_day.AddDays(1)),
            _ => _day < DateOnly.FromDateTime(DateTime.Now));
        OpenFolderCommand = new RelayCommand(_ => Reveal(DayReportFile.Folder));
    }

    /// <summary>Builds today's report and shows it.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Hides it.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Builds the shown day again, for a day still in progress.</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>Looks at the day before.</summary>
    public RelayCommand YesterdayCommand { get; }

    /// <summary>Looks at the day after, up to today.</summary>
    public RelayCommand TomorrowCommand { get; }

    /// <summary>Opens the folder the reports are kept in.</summary>
    public RelayCommand OpenFolderCommand { get; }

    /// <summary>Whether the report is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>The report itself.</summary>
    public string Markdown
    {
        get => _markdown;
        private set => Set(ref _markdown, value);
    }

    /// <summary>Where it was written, shown so the file can be found.</summary>
    public string Path
    {
        get => _path;
        private set => Set(ref _path, value);
    }

    /// <summary>Which day is being shown.</summary>
    public string Day => _day.ToString("dddd d MMMM");

    /// <summary>
    /// Builds one day's report and writes it. Writing on every look is deliberate: a day still in
    /// progress changes, and the file should say what was true when it was last asked rather than
    /// whatever it said the first time.
    /// </summary>
    public void Build(DateOnly day)
    {
        _day = day;

        var entries = DayReport.For(
            day, _runs.Read(), _workstreams(), HandedOverToday, _isLive, DateTimeOffset.Now);

        Markdown = DayReportMarkdown.For(day, entries);
        Path = DayReportFile.Write(day, Markdown) ?? "could not be written";

        Raise(nameof(Day));
    }

    /// <summary>
    /// Whether a workstream's handover was written on the day being reported on. It reads the file's
    /// timestamp, which is why it is here and not in the report itself.
    /// </summary>
    private bool HandedOverToday(string key)
    {
        var workstream = _workstreams().FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));

        if (workstream is null || HandoffPath.For(workstream) is not { } path)
        {
            return false;
        }

        try
        {
            return System.IO.File.Exists(path)
                   && DateOnly.FromDateTime(System.IO.File.GetLastWriteTime(path)) == _day;
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Reveal(string path)
    {
        try
        {
            System.IO.Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
            when (exception is System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            // Nothing to be done about a folder that will not open, and nothing worth saying.
        }
    }
}
