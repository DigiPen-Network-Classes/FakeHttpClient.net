using System.Net.Sockets;
using System.Text;

namespace FakeHttpClient;

public class TcpProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp)
    : ProxyRequest(interactive, url, name, proxyPort, proxyIp)
{
    private TcpClient? _client;
    private Uri? _targetUri;

    protected override async Task PrepareTest(CancellationToken token)
    {
        _targetUri = new Uri(Url);
        _client = new TcpClient();
        await _client.ConnectAsync(ProxyIp, ProxyPort, token);
    }

    protected override async Task ExecuteTest(CancellationToken token)
    {
        if (_client == null || _targetUri == null)
        {
            throw new InvalidOperationException("Client or target URI not initialized.");
        }
        var stream = _client.GetStream();
        var writer = new StreamWriter(stream, Encoding.ASCII);
        var reader = new StreamReader(stream, Encoding.ASCII);

        await writer.WriteAsync($"GET {_targetUri.PathAndQuery} HTTP/1.1\r\n" +
                                $"Host: {_targetUri.Host}\r\n" +
                                "Connection: close\r\n" +
                                "User-Agent: Curl/8.9.1\r\n" +
                                "Accept: */*\r\n" +
                                "\r\n");
        await writer.FlushAsync(token);
        _client.Client.Shutdown(SocketShutdown.Send);

        // Read the raw response until the proxy closes the connection.
        // Errors (e.g. a reset instead of a graceful close) propagate to ProxyRequest, which reports them as a failure.
        var buffer = new char[128];
        int bytesRead;
        while ((bytesRead = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            BytesReceived += bytesRead;
            await Writer.WriteAsync(new string(buffer, 0, bytesRead));
        }
    }

    public override void Dispose()
    {
        _client?.Dispose();
        base.Dispose();
    }
}
