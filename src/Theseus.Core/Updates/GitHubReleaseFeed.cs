using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Updates;

/// <summary>
/// Reads the newest release of the public Theseus repository from GitHub, with no token: the
/// releases are public precisely so that nothing secret has to sit on the machine to check them
/// (Nigel, 2026-10-28). The CI workflow publishes Theseus.msi and SHA256SUMS.txt on every release.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed, IDisposable
{
    /// <summary>The release the feed asks for.</summary>
    public const string LatestEndpoint = "https://api.github.com/repos/nigelbasel/Theseus/releases/latest";

    /// <summary>
    /// The newest releases, pre-releases among them, asked for when pre-releases are included. One call, like
    /// <see cref="LatestEndpoint"/>, so the update check's budget of calls is unchanged.
    /// </summary>
    public const string ListEndpoint = "https://api.github.com/repos/nigelbasel/Theseus/releases?per_page=20";

    /// <summary>The installer's name on every release.</summary>
    public const string InstallerAsset = "Theseus.msi";

    /// <summary>The checksum file's name on every release.</summary>
    public const string ChecksumsAsset = "SHA256SUMS.txt";

    private readonly HttpClient _http;
    private readonly Func<bool> _includePreReleases;

    /// <summary>
    /// Creates the feed, presenting itself as this build of Theseus. <paramref name="includePreReleases"/> is read on
    /// every check, so changing the setting needs no restart.
    /// </summary>
    public GitHubReleaseFeed(string currentVersion, Func<bool>? includePreReleases = null)
        : this(currentVersion, includePreReleases, new HttpClientHandler())
    {
    }

    /// <summary>The same feed over a given handler, so a test can see which address each check asks.</summary>
    public GitHubReleaseFeed(string currentVersion, Func<bool>? includePreReleases, HttpMessageHandler handler)
    {
        _includePreReleases = includePreReleases ?? (() => false);
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Theseus",
            string.IsNullOrWhiteSpace(currentVersion) ? "unknown" : currentVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <inheritdoc />
    public async Task<AvailableRelease?> LatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            var includePreReleases = _includePreReleases();
            using var response = await _http.GetAsync(includePreReleases ? ListEndpoint : LatestEndpoint, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TheseusLog.Debug("No Theseus release is published yet, or the repository is not public");

                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                TheseusLog.Information("The update check got {Status} from GitHub", (int)response.StatusCode);

                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            return includePreReleases ? Newest(json) : Parse(json);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            TheseusLog.Information("The update check could not reach GitHub: {Reason}", exception.Message);

            return null;
        }
    }

    /// <summary>
    /// Reads a GitHub release document. Null when it has no installer, a draft or prerelease flag,
    /// or a tag that is not a version.
    /// </summary>
    public static AvailableRelease? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        return Read(document.RootElement, allowPreRelease: false);
    }

    /// <summary>
    /// The newest usable release in a GitHub release list, pre-release or not, by its build stamp. Null when the list
    /// holds none.
    /// </summary>
    public static AvailableRelease? Newest(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return document.RootElement.EnumerateArray()
            .Select(release => Read(release, allowPreRelease: true))
            .OfType<AvailableRelease>()
            .MaxBy(release => BuildStamp.BuiltAt(release.Version));
    }

    private static AvailableRelease? Read(JsonElement root, bool allowPreRelease)
    {
        if (Flag(root, "draft") || (!allowPreRelease && Flag(root, "prerelease"))
            || Text(root, "tag_name") is not { } tag || Text(root, "html_url") is not { } page)
        {
            return null;
        }

        var version = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;

        if (BuildStamp.BuiltAt(version) is null)
        {
            return null;
        }

        Uri? installer = null;
        Uri? checksums = null;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (Text(asset, "browser_download_url") is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    continue;
                }

                switch (Text(asset, "name"))
                {
                    case InstallerAsset:
                        installer = uri;
                        break;
                    case ChecksumsAsset:
                        checksums = uri;
                        break;
                }
            }
        }

        return installer is null ? null : new AvailableRelease(version, installer, checksums, new Uri(page));
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken) =>
        await _http.GetByteArrayAsync(uri, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
