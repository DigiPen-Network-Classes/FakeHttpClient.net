using System.Net;
using System.Text;

namespace FakeHttpClient
{
    public class HttpProxyRequest : ProxyRequest
    {
        private HttpClientHandler? _handler;
        private HttpRequestMessage? _request;

        public HttpProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp)
        : base(interactive,
            interactive ? WriterFactory.BuildConsoleWriter() : WriterFactory.BuildFileWriter(url, name),
            url,
            name,
            proxyPort,
            proxyIp)
        {
        }

        protected override Task PrepareTest(CancellationToken token)
        {
            _handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://{ProxyIp}:{ProxyPort}"),
                UseProxy = true,
                AllowAutoRedirect = false, // do NOT follow redirects
                UseCookies = false, // do NOT use cookies
            };
            _request = new HttpRequestMessage(HttpMethod.Get, Url)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };

            // Set headers
            _request.Headers.Host = new Uri(Url).Host;
            _request.Headers.UserAgent.ParseAdd("Curl/8.9.1");
            _request.Headers.Accept.ParseAdd("*/*");
            _request.Headers.ConnectionClose = true; // Equivalent to KeepAlive = false

            return Task.CompletedTask;
        }

        protected override async Task ExecuteTest(CancellationToken token)
        {
            if (_request == null || _handler == null)
            {
                throw new InvalidOperationException("Request not initialized. Call PrepareTest first.");
            }

            using var client = new HttpClient(_handler, disposeHandler: true);

            try
            {
                using var response = await client.SendAsync(_request, HttpCompletionOption.ResponseHeadersRead, token);
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var reader = new StreamReader(stream, Encoding.ASCII);
                var buf = new char[64];
                int bytesRead;
                while ((bytesRead = await reader.ReadAsync(buf, 0, buf.Length)) > 0)
                {
                    BytesReceived += bytesRead;
                    token.ThrowIfCancellationRequested();
                    await Writer.WriteAsync(new string(buf, 0, bytesRead));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
