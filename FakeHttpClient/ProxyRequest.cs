using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    public class ProxyRequest : IDisposable
    {
        private const long ThresholdMilliseconds = 7 * 1000; // seconds, longer than that indicates a problem
        private readonly string _url;
        private readonly string _name;
        private readonly TextWriter _writer;
        private readonly int _proxyPort;
        private readonly string _proxyIp;

        public ProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp)
        {
            _url = url;
            _name = name;
            _proxyPort = proxyPort;
            _proxyIp = proxyIp;
            _writer = interactive ? WriterFactory.BuildConsoleWriter() : WriterFactory.BuildFileWriter(url, name);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var request = (HttpWebRequest)WebRequest.Create(_url);
            request.Host = new Uri(_url).Host;
            request.ProtocolVersion = HttpVersion.Version11;
            request.Proxy = new WebProxy($"http://{_proxyIp}:{_proxyPort}");;
            request.UserAgent = "Curl/8.9.1"; // lie about who we are
            request.Method = "GET";
            request.Accept = "*/*";
            request.AllowReadStreamBuffering = false; // disables buffering so we can see the delay in the response
            request.KeepAlive = false; // do NOT keep alive

            try
            {
                await _writer.WriteLineAsync($"Begin Test {_name}: {_url} {DateTime.Now}");
                var sw = Stopwatch.StartNew();
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream() ?? Stream.Null)
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var buf = new char[64];
                    int bytesRead;
                    while ((bytesRead = await reader.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        await _writer.WriteAsync(new string(buf, 0, bytesRead));
                    }
                }

                sw.Stop();
                var result = sw.ElapsedMilliseconds < ThresholdMilliseconds ? "pass!" : "FAIL (Took too long!)";
                await _writer.WriteLineAsync($"\nEnd Test: Total time for {_name} ({_url}): {sw.ElapsedMilliseconds}ms. Result: {result}");
            }
            catch (WebException ex)
            {
                await _writer.WriteLineAsync($"Error: {ex.Message}");
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream() ?? Stream.Null))
                    {
                        await _writer.WriteLineAsync(await reader.ReadToEndAsync());
                    }
                }

                throw;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _writer?.Dispose();
        }
    }
}
