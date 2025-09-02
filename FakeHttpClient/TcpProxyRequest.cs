using System.Net.Sockets;
using System.Text;

namespace FakeHttpClient;

public class TcpProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp)
    : ProxyRequest(interactive, url, name, proxyPort, proxyIp)
{
    private TcpClient? _client;
    private Uri? _targetUri;

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

        // Read raw response byte-by-byte
        var buffer = new char[128];
        while (!token.IsCancellationRequested)
        {
            try
            {
                var bytesRead = await reader.ReadAsync(buffer, 0, 128);
                if (bytesRead > 0)
                {
                    BytesReceived += bytesRead;
                    var s = new string(buffer, 0, bytesRead);
                    await Writer.WriteAsync(s);
                }
                else
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                // Handle gracefully
                Console.WriteLine("Operation canceled.");
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
