using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    public class ProxyRequest
    {
        private const long ThresholdMilliseconds = 7 * 1000; // seconds, longer than that indicates a problem
        private readonly string _url;
        private readonly string _name;
        private readonly bool _interactive;
        private readonly int _proxyPort;
        private readonly string _proxyIp;

        public ProxyRequest(string url, string name, bool interactive, int proxyPort, string proxyIp)
        {
            _url = url;
            _name = name;
            _interactive = interactive;
            _proxyPort = proxyPort;
            _proxyIp = proxyIp;
        }

        public async Task ExecuteAsync()
        {
            //var proxyIp = "10.211.55.3"; // "127.0.0.1"
            var request = (HttpWebRequest)WebRequest.Create(_url);
            request.Host = new Uri(_url).Host;
            request.ProtocolVersion = HttpVersion.Version10;
            request.Proxy = new WebProxy($"http://{_proxyIp}:{_proxyPort}");;
            request.UserAgent = "Curl/8.9.1"; // lie about who we are
            request.Method = "GET";
            request.Accept = "*/*";
            request.AllowReadStreamBuffering = false; // disables buffering so we can see the delay in the response
            request.KeepAlive = false; // do NOT keep alive

            try
            {
                var interactiveText = _interactive ? "[interactive]" : "[non-interactive]";
                Console.WriteLine($"Begin Test {_name}: {_url} {interactiveText}: {DateTime.Now.ToLongTimeString()}");
                var sw = Stopwatch.StartNew();
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream() ?? Stream.Null)
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var buf = new char[64];
                    int bytesRead;
                    while ((bytesRead = await reader.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        Console.Write(new string(buf, 0, bytesRead));
                    }
                }

                sw.Stop();
                var result = sw.ElapsedMilliseconds < ThresholdMilliseconds ? "pass!" : "FAIL (Took too long!)";
                Console.WriteLine($"\nEnd Test: Total time for {_name} ({_url}): {sw.ElapsedMilliseconds}ms. Result: {result}");
            }
            catch (WebException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream() ?? Stream.Null))
                    {
                        Console.WriteLine(await reader.ReadToEndAsync());
                    }
                }
            }
        }
    }
}
