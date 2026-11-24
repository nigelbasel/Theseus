using System.Net;
using System.Net.Http;
using Theseus.App.ViewModels;
using Theseus.Core.Updates;

namespace Theseus.Tests.Updates;

/// <summary>
/// The update's download says how far it has got, from the response's Content-Length, and can be cancelled (baseline
/// WB7; finding C19; W52; checklist G145, G148). A stopped update reads as an error, not as the offer (W46, C17).
/// </summary>
[TestFixture]
public class UpdateDownloadProgressTests
{
    private const string Running = "2026.10.28.20000";

    [Test]
    public async Task TheFeed_ReportsProgress_FromContentLength()
    {
        byte[] body = new byte[300_000];
        using GitHubReleaseFeed feed = new(Running, null, new SizedHandler(body));
        List<double?> reports = [];

        byte[] read = await feed.DownloadAsync(new Uri("https://example.invalid/Theseus.msi"), new Recorder(reports), CancellationToken.None);

        Assert.That(read, Has.Length.EqualTo(body.Length));
        Assert.That(reports, Has.Count.GreaterThan(2), "it reports as it reads, not once at the end");
        Assert.That(reports[0], Is.EqualTo(0));
        Assert.That(reports[^1], Is.EqualTo(1));
        Assert.That(reports, Is.Ordered, "it only moves forward");
    }

    [Test]
    public async Task Cancel_StopsTheDownload_AndInstallsNothing()
    {
        int installs = 0;
        HangingFeed feed = new();
        UpdateViewModel updates = new(feed, Running, Path.GetTempPath(), _ => installs++);
        await updates.CheckAsync();

        updates.UpdateCommand.Execute(null);
        await feed.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(updates.IsDownloading, Is.True);
        Assert.That(updates.CancelDownloadCommand.CanExecute(null), Is.True);

        updates.CancelDownloadCommand.Execute(null);
        await Until(() => !updates.IsDownloading);

        Assert.That(installs, Is.Zero);
        Assert.That(updates.Status, Is.EqualTo("The update was cancelled. Nothing was installed."));
        Assert.That(updates.IsOffered, Is.True, "the offer stays, to try again");
        Assert.That(updates.Severity, Is.EqualTo(NoticeSeverity.Information));
    }

    [Test]
    public async Task AFailedUpdate_IsAnError()
    {
        HangingFeed feed = new() { Failure = new HttpRequestException("The network is down.") };
        UpdateViewModel updates = new(feed, Running, Path.GetTempPath(), _ => { });
        await updates.CheckAsync();

        updates.UpdateCommand.Execute(null);
        await Until(() => updates.Status.StartsWith("The update stopped", StringComparison.Ordinal));

        Assert.That(updates.Severity, Is.EqualTo(NoticeSeverity.Error));
    }

    private static async Task Until(Func<bool> condition)
    {
        for (int tries = 0; tries < 200 && !condition(); tries++)
        {
            await Task.Delay(25);
        }

        Assert.That(condition(), Is.True, "within five seconds");
    }

    private sealed class Recorder(List<double?> reports) : IProgress<double?>
    {
        public void Report(double? value) => reports.Add(value);
    }

    private sealed class SizedHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }

    private sealed class HangingFeed : IReleaseFeed
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? Failure { get; init; }

        public Task<AvailableRelease?> LatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AvailableRelease?>(new AvailableRelease("2026.10.29.100", new Uri("https://example.invalid/Theseus.msi"),
                new Uri("https://example.invalid/SHA256SUMS.txt"), new Uri("https://example.invalid/")));

        public Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken) =>
            Task.FromResult(System.Text.Encoding.ASCII.GetBytes(new string('0', 64) + "  Theseus.msi\n"));

        public async Task<byte[]> DownloadAsync(Uri uri, IProgress<double?>? progress, CancellationToken cancellationToken)
        {
            if (uri.AbsolutePath.EndsWith(".txt", StringComparison.Ordinal))
            {
                return await DownloadAsync(uri, cancellationToken);
            }

            Started.TrySetResult();

            if (Failure is not null)
            {
                throw Failure;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return [];
        }
    }
}
