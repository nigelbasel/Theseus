using System.Net;
using System.Net.Http;

namespace Theseus.Tests.Updates;

/// <summary>Answers every request with one body, and records the addresses asked.</summary>
public sealed class RecordingHandler(Func<Uri, string> body) : HttpMessageHandler
{
    public List<Uri> Asked { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Asked.Add(request.RequestUri!);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body(request.RequestUri!)) });
    }
}
