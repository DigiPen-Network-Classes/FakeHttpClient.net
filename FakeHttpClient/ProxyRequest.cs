using System.Diagnostics;
using System.Net;

namespace FakeHttpClient
{
    public enum TestResult
    {
        Success,
        /// <summary>
        /// the test(s) took too long
        /// </summary>
        TooSlow,
        /// <summary>
        /// the tests did not receive the expected response
        /// </summary>
        NotEnoughData
    }

    public class ProxyRequestFactory
    {
        /// <summary>
        /// Create the ProxyRequest to run this test based on parameters.
        /// </summary>
        /// <param name="interactive"></param>
        /// <param name="url"></param>
        /// <param name="name"></param>
        /// <param name="proxyPort"></param>
        /// <param name="proxyIp"></param>
        /// <param name="rawRequest"></param>
        /// <returns></returns>
        public static IProxyRequest CreateProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp, bool rawRequest = false)
        {
            if (rawRequest)
            {
                return new TcpProxyRequest(interactive, url, name, proxyPort, proxyIp);
            }
            return new HttpProxyRequest(interactive, url, name, proxyPort, proxyIp);
        }

        /// <summary>
        /// Creates a ProxyRequest that does nothing.
        /// </summary>
        /// <param name="interactive"></param>
        /// <param name="url"></param>
        /// <param name="testName"></param>
        /// <param name="proxyPort"></param>
        /// <param name="proxyIp"></param>
        /// <returns></returns>
        public static IProxyRequest CreateNullProxyRequest(bool interactive, string url, string testName, int proxyPort, string proxyIp)
        {
            return new NullProxyRequest(interactive, url, testName, proxyPort, proxyIp);
        }
    }
    public interface IProxyRequest : IDisposable
    {
        Task ExecuteAsync(CancellationToken token);
    }

    public abstract class ProxyRequest : IProxyRequest
    {
        private const long ThresholdMilliseconds = 7 * 1000; // seconds, longer than that indicates a problem
        private const int MinimumBytesReceived = 100; // less than that indicates a problem

        protected bool Interactive { get; set; }
        protected string Url { get; }
        private string Name { get; }
        protected TextWriter Writer { get; }
        protected int ProxyPort { get; }
        protected string ProxyIp { get; }

        protected int BytesReceived { get; set; }

        protected ProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp) : this(interactive,
            interactive ? WriterFactory.BuildConsoleWriter() : WriterFactory.BuildFileWriter(url, name),
            url, name, proxyPort, proxyIp)
        {
        }

        protected ProxyRequest(bool interactive, TextWriter writer, string url, string name, int proxyPort, string proxyIp)
        {
            Url = url;
            Name = name;
            ProxyPort = proxyPort;
            ProxyIp = proxyIp;
            Interactive = interactive;
            Writer = writer;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await PrepareTest(token);
            try
            {
                await Writer.WriteLineAsync($"Begin Test {Name}: {Url} {DateTime.Now}");
                var sw = Stopwatch.StartNew();

                await ExecuteTest(token);
                sw.Stop();
                var result = EvaluateResult(sw.ElapsedMilliseconds);
                var pass = result == TestResult.Success ? "pass!" : $"FAIL! ({result})";
                await Writer.WriteLineAsync($"\nEnd Test: Total time for {Name} ({Url}): {sw.ElapsedMilliseconds}ms. Result: {pass}");
            }
            catch (Exception e)
            {
                await HandleException(e);
                throw;
            }
        }

        protected abstract Task PrepareTest(CancellationToken token);
        protected abstract Task ExecuteTest(CancellationToken token);

        private TestResult EvaluateResult(long elapsedMilliseconds)
        {
            if (elapsedMilliseconds >= ThresholdMilliseconds)
            {
                return TestResult.TooSlow;
            }
            return BytesReceived < MinimumBytesReceived ? TestResult.NotEnoughData : TestResult.Success;
        }

        private async Task HandleException(Exception e)
        {
            await Writer.WriteLineAsync($"Error: {e.Message}");
            if (e is WebException webEx && webEx.Response != null)
            {
                using var reader = new StreamReader(webEx.Response.GetResponseStream());
                await Writer.WriteLineAsync(await reader.ReadToEndAsync());
            }
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            Writer.Dispose();
        }
    }
}
