using System.IO;
using System.Net.Http;
using Theseus.Core.Diagnostics;
using Theseus.Core.Updates;

namespace Theseus.App.ViewModels;

/// <summary>
/// Offers a newer published build (Nigel, 2026-10-28: "checks its version and offers the user a
/// chance to upgrade"). It only ever offers: nothing downloads or installs until Update is pressed,
/// and Later puts that version aside until a newer one appears.
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
        }, _ => _offer is not null && !_isBusy);
    }

    /// <summary>The release on offer, or null.</summary>
    public AvailableRelease? Offer
    {
        get => _offer;
        private set
        {
            if (Set(ref _offer, value))
            {
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

    /// <summary>Puts the offered version aside until a newer one is published.</summary>
    public RelayCommand LaterCommand { get; }

    /// <summary>Asks the feed for the newest release, and offers it when it is newer and not put aside.</summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy)
        {
            return;
        }

        var latest = await _feed.LatestAsync(cancellationToken);

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

    private async Task UpdateAsync()
    {
        if (_offer is not { } release)
        {
            return;
        }

        SetBusy(true);
        Status = $"Downloading Theseus {release.Version}…";

        try
        {
            var path = await UpdateDownloader.FetchAsync(_feed, release, _downloadDirectory, CancellationToken.None);

            Status = "Installing: Windows will ask for permission, then Theseus restarts.";
            _install(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or HttpRequestException
                                              or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "The update to {Version} stopped", release.Version);
            Status = $"The update stopped: {exception.Message}";
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
