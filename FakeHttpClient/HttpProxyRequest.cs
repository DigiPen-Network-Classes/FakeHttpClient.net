using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    public class HttpProxyRequest : ProxyRequest
    {
        private HttpWebRequest _request;

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
            _request = (HttpWebRequest)WebRequest.Create(Url);
            _request.Host = new Uri(Url).Host;
            _request.ProtocolVersion = HttpVersion.Version11;
            _request.Proxy = new WebProxy($"http://{ProxyIp}:{ProxyPort}");
            _request.UserAgent = "Curl/8.9.1"; // lie about who we are
            _request.Method = "GET";
            _request.Accept = "*/*";
            _request.AllowReadStreamBuffering = false; // disables buffering so we can see the delay in the response
            _request.KeepAlive = false; // do NOT keep alive

            return Task.CompletedTask;
        }

        protected override async Task ExecuteTest(CancellationToken token)
        {
            using (var response = (HttpWebResponse)await _request.GetResponseAsync())
            using (var stream = response.GetResponseStream() ?? Stream.Null)
            using (var reader = new StreamReader(stream, Encoding.ASCII))
            {
                var buf = new char[64];
                int bytesRead;
                while ((bytesRead = await reader.ReadAsync(buf, 0, buf.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    await Writer.WriteAsync(new string(buf, 0, bytesRead));
                }
            }
        }
    }
}
