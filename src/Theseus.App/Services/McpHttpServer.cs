using System.IO;
using System.Net;
using System.Text;
using Theseus.Core.Diagnostics;
using Theseus.Core.Mcp;

namespace Theseus.App.Services;

/// <summary>
/// The session time server on the wire: MCP's Streamable HTTP transport, on localhost only, at
/// <c>/mcp/&lt;session id&gt;</c>. POST carries every message and gets one JSON object back; there is no
/// event stream, so GET is refused, which the transport allows.
///
/// A request with an Origin from anywhere but this machine is refused, as the transport requires, so a
/// web page cannot reach it by DNS rebinding. If the port cannot be had, Theseus carries on without it
/// and sessions are started without the server rather than pointed at nothing.
/// </summary>
public sealed class McpHttpServer : IAsyncDisposable
{
    private const string Path = "/mcp/";
    private const long LargestBody = 1024 * 1024;

    private readonly McpHandler _handler;
    private readonly HttpListener _listener = new();
    private Task? _loop;

    /// <summary>Creates a server for a handler on one port.</summary>
    public McpHttpServer(McpHandler handler, int port)
    {
        _handler = handler;
        Port = port;
        _listener.Prefixes.Add($"http://localhost:{port}{Path}");
    }

    /// <summary>The port it listens on.</summary>
    public int Port { get; }

    /// <summary>Starts listening, or says why it could not and returns false.</summary>
    public bool Start()
    {
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException exception)
        {
            TheseusLog.Warning(exception, "The session time server could not listen on port {Port}; sessions will start without it", Port);

            return false;
        }

        _loop = Task.Run(ListenAsync);
        TheseusLog.Information("Serving session time tools at http://localhost:{Port}{Path}", Port, Path);

        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();

        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }
    }

    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(context));
        }
    }

    private async Task ServeAsync(HttpListenerContext context)
    {
        var response = context.Response;

        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath ?? string.Empty;
            var sessionId = path.StartsWith(Path, StringComparison.Ordinal) ? Uri.UnescapeDataString(path[Path.Length..].Trim('/')) : string.Empty;

            if (!IsLocal(request.Headers["Origin"]))
            {
                response.StatusCode = 403;
                return;
            }

            if (sessionId.Length == 0 || sessionId.Contains('/'))
            {
                response.StatusCode = 404;
                return;
            }

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 405;
                response.AddHeader("Allow", "POST");
                return;
            }

            if (request.ContentLength64 > LargestBody)
            {
                response.StatusCode = 413;
                return;
            }

            string body;

            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var answer = _handler.Handle(sessionId, body, request.Headers["MCP-Protocol-Version"]);

            response.StatusCode = answer.StatusCode;

            if (answer.Body is { } json)
            {
                var bytes = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json";
                response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or HttpListenerException or InvalidOperationException or ArgumentException)
        {
            TheseusLog.Debug("A session time request failed: {Reason}", exception.Message);
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException or InvalidOperationException)
            {
                TheseusLog.Debug("A session time response could not be closed: {Reason}", exception.Message);
            }
        }
    }

    private static bool IsLocal(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || origin == "null")
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;
    }
}
