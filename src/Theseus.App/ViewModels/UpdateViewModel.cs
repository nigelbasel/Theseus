using System.IO;
using System.Net.Http;
using Theseus.Core.Diagnostics;
using Theseus.Core.Updates;

namespace Theseus.App.ViewModels;

/// <summary>
/// Offers a newer published build (Nigel, 2026-10-28: "checks its version and offers the user a
/// chance to upgrade"). It only ever offers: nothing downloads or installs until Update is pressed,
/// and Skip this version puts that version aside until a newer one appears. The download shows how far it has got and
/// can be cancelled (baseline W52; finding C19), and a stopped update shows as an error, not as the offer (W46, C17).
/// </summary>
public sealed class UpdateViewModel : ObservableObject
{
    private readonly IReleaseFeed _feed;
    private readonly string _currentVersion;
    private readonly string _downloadDirectory;
    private readonly Action<string> _install;
    private AvailableRelease? _offer;
    private string? _putAside;
    private string _status = string.Empty;
    private bool _isBusy;
    private CancellationTokenSource? _download;
    private double? _progress;
    private NoticeSeverity _severity;

    /// <summary>Creates the offer.</summary>
    /// <param name="feed">Where releases are read from.</param>
    /// <param name="currentVersion">The running build's stamp.</param>
    /// <param name="downloadDirectory">Where a verified installer is saved before it runs.</param>
    /// <param name="install">Runs a verified installer and closes Theseus, given its path.</param>
    public UpdateViewModel(IReleaseFeed feed, string currentVersion, string downloadDirectory, Action<string> install)
    {
        _feed = feed;
        _currentVersion = currentVersion;
        _downloadDirectory = downloadDirectory;
        _install = install;

        UpdateCommand = new RelayCommand(async _ => await UpdateAsync(), _ => _offer is not null && !_isBusy);
        LaterCommand = new RelayCommand(_ =>
        {
            _putAside = _offer?.Version;
            Offer = null;
            Status = string.Empty;
            Severity = NoticeSeverity.Information;
        }, _ => _offer is not null && !_isBusy);
        CheckNowCommand = new RelayCommand(async _ => await CheckNowAsync(), _ => !_isBusy);
        CancelDownloadCommand = new RelayCommand(_ => _download?.Cancel(), _ => _download is { IsCancellationRequested: false });
    }

    /// <summary>Stops the download. Nothing has been installed, and the offer stays.</summary>
    public RelayCommand CancelDownloadCommand { get; }

    /// <summary>Whether the installer is downloading, which shows the progress bar and Cancel.</summary>
    public bool IsDownloading => _download is not null;

    /// <summary>How far the download has got, from 0 to 1, or null while its size is not known.</summary>
    public double? DownloadProgress
    {
        get => _progress;
        private set
        {
            if (Set(ref _progress, value))
            {
                Raise(nameof(IsProgressKnown));
                Raise(nameof(DownloadPercent));
            }
        }
    }

    /// <summary>Whether the download's size is known, so the bar can fill rather than run.</summary>
    public bool IsProgressKnown => _progress is not null;

    /// <summary>The download's progress as a percentage, for the bar.</summary>
    public double DownloadPercent => (_progress ?? 0) * 100;

    /// <summary>Information while the offer stands, Error once an update has stopped (W46).</summary>
    public NoticeSeverity Severity
    {
        get => _severity;
        private set => Set(ref _severity, value);
    }

    /// <summary>Asks the feed now, from Help, and says what it found whatever that was.</summary>
    public RelayCommand CheckNowCommand { get; }

    /// <summary>Where a check asked for by hand reports: up to date, an update, or that it failed.</summary>
    public Action<string>? Said { get; set; }

    /// <summary>The release on offer, or null.</summary>
    public AvailableRelease? Offer
    {
        get => _offer;
        private set
        {
            if (Set(ref _offer, value))
            {
                Severity = NoticeSeverity.Information;
                Raise(nameof(IsOffered));
                Raise(nameof(OfferText));
            }
        }
    }

    /// <summary>Whether a newer build is on offer.</summary>
    public bool IsOffered => _offer is not null;

    /// <summary>What the offer says.</summary>
    public string OfferText => _offer is null
        ? string.Empty
        : $"Theseus {_offer.Version} is available (you have {_currentVersion}).";

    /// <summary>What the update is doing, or why it stopped.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>Downloads, verifies and installs the offered build, then restarts Theseus.</summary>
    public RelayCommand UpdateCommand { get; }

    /// <summary>Skip this version: puts the offered version aside until a newer one is published.</summary>
    public RelayCommand LaterCommand { get; }

    /// <summary>Asks the feed for the newest release, and offers it when it is newer and not put aside.</summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy)
        {
            return;
        }

        var latest = await _feed.LatestAsync(cancellationToken);

        // Null is also a failed check, so only an answer that is not newer withdraws an offer: one made with
        // pre-releases included, after the setting was turned off.
        if (latest is not null && !UpdatePolicy.IsNewer(_currentVersion, latest.Version))
        {
            Offer = null;
        }

        if (latest is null || !UpdatePolicy.IsNewer(_currentVersion, latest.Version)
            || string.Equals(latest.Version, _putAside, StringComparison.Ordinal))
        {
            return;
        }

        if (_offer?.Version != latest.Version)
        {
            TheseusLog.Information("Theseus {Version} is published; this is {Current}", latest.Version, _currentVersion);
            Offer = latest;
        }
    }

    /// <summary>
    /// The check behind Help, Check for updates. Unlike the timed check it ignores a version put aside
    /// with Later, since asking by hand is changing your mind, and it always answers: a failed check
    /// must not read as "up to date".
    /// </summary>
    public async Task<string> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        string answer;

        try
        {
            var latest = await _feed.LatestAsync(cancellationToken);

            if (latest is null)
            {
                answer = "No published release of Theseus was found.";
            }
            else if (!UpdatePolicy.IsNewer(_currentVersion, latest.Version))
            {
                Offer = null;
                answer = $"Theseus {_currentVersion} is up to date.";
            }
            else
            {
                _putAside = null;
                Status = string.Empty;
                Offer = latest;
                answer = $"Theseus {latest.Version} is available.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException
                                              or InvalidDataException or System.Text.Json.JsonException)
        {
            TheseusLog.Warning(exception, "Checking for updates failed");
            answer = $"Could not check for updates: {exception.Message}";
        }

        TheseusLog.Information("Check for updates: {Answer}", answer);
        Said?.Invoke(answer);

        return answer;
    }

    private async Task UpdateAsync()
    {
        if (_offer is not { } release)
        {
            return;
        }

        SetBusy(true);
        Severity = NoticeSeverity.Information;
        Status = $"Downloading Theseus {release.Version}…";
        using CancellationTokenSource download = new();
        StartDownload(download);

        try
        {
            string path = await UpdateDownloader.FetchAsync(_feed, release, _downloadDirectory, download.Token, new Reporter(this));

            EndDownload();
            Status = "Installing: Windows will ask for permission, then Theseus restarts.";
            _install(path);
        }
        catch (OperationCanceledException) when (download.IsCancellationRequested)
        {
            TheseusLog.Information("The update to {Version} was cancelled", release.Version);
            Status = "The update was cancelled. Nothing was installed.";
            SetBusy(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or HttpRequestException
                                              or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The update to {Version} stopped", release.Version);
            Severity = NoticeSeverity.Error;
            Status = $"The update stopped: {exception.Message}";
            SetBusy(false);
        }
        finally
        {
            // Whatever ended it, Cancel must not reach a disposed source.
            EndDownload();
        }
    }

    private void StartDownload(CancellationTokenSource download)
    {
        _download = download;
        DownloadProgress = null;
        Raise(nameof(IsDownloading));
    }

    private void EndDownload()
    {
        _download = null;
        Raise(nameof(IsDownloading));
    }

    /// <summary>The installer could not be started: said as an error, with the update offered again, and Theseus left running.</summary>
    public void InstallFailed(string why)
    {
        Severity = NoticeSeverity.Error;
        Status = $"{why} Nothing was installed, and Theseus keeps running.";
        SetBusy(false);
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    // Reports at once, on whatever thread the download reads on: Progress<T> would post to the dispatcher, and a bound
    // double needs no dispatcher.
    private sealed class Reporter(UpdateViewModel owner) : IProgress<double?>
    {
        public void Report(double? value) => owner.DownloadProgress = value;
    }
}
