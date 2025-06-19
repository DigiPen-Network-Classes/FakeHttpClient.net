using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FakeHttpClient
{
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

            using (var stream = _client.GetStream())
            using (var writer = new StreamWriter(stream, Encoding.ASCII))
            using (var reader = new StreamReader(stream, Encoding.ASCII))
            {
                await writer.WriteAsync($"GET {_targetUri.PathAndQuery} HTTP/1.1\r\n" +
                                        $"Host: {_targetUri.Host}\r\n" +
                                        "Connection: close\r\n" +
                                        "User-Agent: Curl/8.9.1\r\n" +
                                        "Accept: */*\r\n" +
                                        "\r\n\r\n");
                await writer.FlushAsync();
                // Read raw response byte-by-byte
                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await Writer.WriteAsync(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                }
            }
        }
    }
}
