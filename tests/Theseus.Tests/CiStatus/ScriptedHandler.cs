using System.Net.Http;

namespace Theseus.Tests.CiStatus;

/// <summary>Answers each request with the next scripted response, and keeps every request it was sent.</summary>
public sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] answers) : HttpMessageHandler
{
    private int _next;

    public List<HttpRequestMessage> Sent { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Sent.Add(request);

        if (_next >= answers.Length)
        {
            throw new InvalidOperationException($"Request {_next + 1} was not scripted: {request.RequestUri}");
        }

        return Task.FromResult(answers[_next++](request));
    }
}
