using System.Net.Sockets;
using System.Text;

namespace FakeHttpClient;

public class TcpProxyRequest : ProxyRequest
{
    private TcpClient? _client;
    private Uri? _targetUri;

    public TcpProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp)
        : base(interactive, url, name, proxyPort, proxyIp)
    {
    }

    protected override Task PrepareTest(CancellationToken token)
    {
        _client = new TcpClient(ProxyIp, ProxyPort);
        _targetUri = new Uri(Url);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteTest(CancellationToken token)
    {
        if (_client == null || _targetUri == null)
        {
            throw new InvalidOperationException("Client or target URI not initialized.");
        }

        await using var stream = _client.GetStream();
        await using var writer = new StreamWriter(stream, Encoding.ASCII);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        await writer.WriteAsync($"GET {_targetUri.PathAndQuery} HTTP/1.1\r\n" +
                                $"Host: {_targetUri.Host}\r\n" +
                                "Connection: close\r\n" +
                                "User-Agent: Curl/8.9.1\r\n" +
                                "Accept: */*\r\n" +
                                "\r\n");
        await writer.FlushAsync(token);
        // Read raw response byte-by-byte
        var buffer = new byte[128];
        while (!token.IsCancellationRequested)
        {
            try
            {
                Console.WriteLine("calling read async");
                var bytesRead = await stream.ReadAsync(buffer, token);
                Console.WriteLine($"read {bytesRead} bytes");

                if (bytesRead > 0)
                {
                    await Writer.WriteAsync(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                }
                await Task.Delay(50, token);
                Console.WriteLine("loop");
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                Console.WriteLine("Operation cancelled.");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from stream: {ex.Message}");
                break;
            }
        }
    }
}
