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

    /// <summary>The installer's name on every release.</summary>
    public const string InstallerAsset = "Theseus.msi";

    /// <summary>The checksum file's name on every release.</summary>
    public const string ChecksumsAsset = "SHA256SUMS.txt";

    private readonly HttpClient _http;

    /// <summary>Creates the feed, presenting itself as this build of Theseus.</summary>
    public GitHubReleaseFeed(string currentVersion)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Theseus",
            string.IsNullOrWhiteSpace(currentVersion) ? "unknown" : currentVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <inheritdoc />
    public async Task<AvailableRelease?> LatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(LatestEndpoint, cancellationToken);

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

            return Parse(await response.Content.ReadAsStringAsync(cancellationToken));
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
        var root = document.RootElement;

        if (Flag(root, "draft") || Flag(root, "prerelease")
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
