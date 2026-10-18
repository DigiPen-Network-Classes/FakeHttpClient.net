using System.Net.Sockets;
using System.Text;

namespace FakeHttpClient;

public class TcpProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp, RequestMode mode = RequestMode.Normal)
    : ProxyRequest(interactive, url, name, proxyPort, proxyIp)
{
    /// <summary>
    /// Pause between pieces of a split request: long enough that each piece arrives as its own recv().
    /// </summary>
    private static readonly TimeSpan SplitDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Padding for a large request: 20 headers of about 500 bytes each, roughly 10 KB in total.
    /// Each header stays well under per-header limits (nginx and Apache allow about 8 KB per line).
    /// </summary>
    private const int PaddingHeaderCount = 20;
    private const int PaddingHeaderValueLength = 480;

    /// <summary>
    /// Write at most this much of the response to the output; large responses are still counted and hashed.
    /// </summary>
    private const int OutputLimitBytes = 1024 * 1024;

    private TcpClient? _client;

    /// <summary>
    /// Connect, then send nothing for this long before sending the request.
    /// </summary>
    public int StallMs { get; init; }

    protected override int DeliberateDelayMs => StallMs;

    protected override string Description =>
        (mode == RequestMode.Normal ? "" : $" ({mode})") + (StallMs > 0 ? $" (stall {StallMs}ms before sending)" : "");
    private Uri? _targetUri;

    protected override async Task PrepareTest(CancellationToken token)
    {
        _targetUri = new Uri(Url);
        // send each piece of a split request immediately instead of letting Nagle combine them
        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(ProxyIp, ProxyPort, token);
    }

    protected override async Task ExecuteTest(CancellationToken token)
    {
        if (_client == null || _targetUri == null)
        {
            throw new InvalidOperationException("Client or target URI not initialized.");
        }
        var stream = _client.GetStream();

        // A stalled client: connected, but silent. The proxy must not let this hold up other clients.
        if (StallMs > 0)
        {
            await Task.Delay(StallMs, token);
        }
        var writer = new StreamWriter(stream, Encoding.ASCII);

        var request = $"GET {_targetUri.PathAndQuery} HTTP/1.1\r\n" +
                      $"Host: {_targetUri.Host}\r\n" +
                      "Connection: close\r\n" +
                      "User-Agent: Curl/8.9.1\r\n" +
                      "Accept: */*\r\n" +
                      (mode.HasFlag(RequestMode.LargeRequest) ? PaddingHeaders() : "") +
                      "\r\n";

        var pieces = mode.HasFlag(RequestMode.SplitRequest) ? SplitRequest(request) : [request];
        for (var i = 0; i < pieces.Count; i++)
        {
            if (i > 0)
            {
                await Task.Delay(SplitDelay, token);
            }
            await writer.WriteAsync(pieces[i]);
            await writer.FlushAsync(token);
        }

        if (!mode.HasFlag(RequestMode.NoHalfClose))
        {
            _client.Client.Shutdown(SocketShutdown.Send);
        }

        // Read the raw response until the proxy closes the connection, parsing and hashing it as it arrives.
        // Errors (e.g. a reset instead of a graceful close) propagate to ProxyRequest, which reports them as a failure.
        Response = new HttpResponseInspector();
        var buffer = new byte[64 * 1024];
        var written = 0;
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, token)) > 0)
        {
            BytesReceived += bytesRead;
            Response.Feed(buffer.AsSpan(0, bytesRead));
            if (written < OutputLimitBytes)
            {
                var show = Math.Min(bytesRead, OutputLimitBytes - written);
                await Writer.WriteAsync(Encoding.ASCII.GetString(buffer, 0, show));
                written += show;
                if (written == OutputLimitBytes)
                {
                    await Writer.WriteLineAsync($"\n... (output stops after {OutputLimitBytes} bytes; the rest is still counted and checked)");
                }
            }
        }
    }

    /// <summary>
    /// Build the padding headers for a large request. The values are deterministic so a byte-exact
    /// echo of the request can be compared later.
    /// </summary>
    private static string PaddingHeaders()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var sb = new StringBuilder();
        for (var i = 1; i <= PaddingHeaderCount; i++)
        {
            sb.Append($"X-CS260-Padding-{i:D2}: ");
            for (var j = 0; j < PaddingHeaderValueLength; j++)
            {
                sb.Append(alphabet[(i * 7 + j) % alphabet.Length]);
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Split the request into pieces that break the request line, the Host header, and the final
    /// "\r\n\r\n" (sent as "\r\n\r" + "\n"), so a proxy must keep reading until it sees the whole terminator.
    /// </summary>
    private static List<string> SplitRequest(string request)
    {
        var cuts = new[]
        {
            request.IndexOf(" HTTP/1.1", StringComparison.Ordinal) + 5, // inside "HTTP/1.1"
            request.IndexOf("Host: ", StringComparison.Ordinal) + 3,    // inside "Host"
            request.Length - 1,                                         // before the last "\n"
        };
        var pieces = new List<string>();
        var start = 0;
        foreach (var cut in cuts)
        {
            pieces.Add(request[start..cut]);
            start = cut;
        }
        pieces.Add(request[start..]);
        return pieces;
    }

    public override void Dispose()
    {
        _client?.Dispose();
        Response?.Dispose();
        base.Dispose();
    }
}
