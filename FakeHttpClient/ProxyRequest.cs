using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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
        NotEnoughData,
        /// <summary>
        /// the proxy reset the connection (RST) instead of closing it gracefully
        /// </summary>
        ConnectionReset,
        /// <summary>
        /// some other error occurred while talking to the proxy
        /// </summary>
        Error,
        /// <summary>
        /// the overall timeout was reached before the proxy closed the connection
        /// </summary>
        Timeout
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
        /// <param name="mode">how the raw TCP client sends its request (ignored by the HttpClient version)</param>
        /// <returns></returns>
        public static IProxyRequest CreateProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp, bool rawRequest = false, RequestMode mode = RequestMode.Normal)
        {
            if (rawRequest)
            {
                return new TcpProxyRequest(interactive, url, name, proxyPort, proxyIp, mode);
            }
            if (mode != RequestMode.Normal)
            {
                Console.WriteLine($"WARNING: mode {mode} requires --raw true; ignoring it for {url}");
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

        /// <summary>
        /// Extra detail for the Begin Test line, e.g. the request mode.
        /// </summary>
        protected virtual string Description => "";

        /// <summary>
        /// Set by subclasses when the transfer ended because of an error rather than a graceful close.
        /// </summary>
        protected Exception? TransportError { get; set; }

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
            await Writer.WriteLineAsync($"Begin Test {Name}: {Url}{Description} {DateTime.Now}");
            var sw = Stopwatch.StartNew();
            try
            {
                await PrepareTest(token);
                await ExecuteTest(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // reported as Timeout below
            }
            catch (Exception e)
            {
                TransportError ??= e;
            }
            sw.Stop();

            var result = EvaluateResult(sw.ElapsedMilliseconds, token.IsCancellationRequested);
            if (TransportError != null)
            {
                await HandleException(TransportError);
            }
            var pass = result == TestResult.Success ? "pass!" : $"FAIL! ({result})";
            await Writer.WriteLineAsync($"\nEnd Test: Total time for {Name} ({Url}): {sw.ElapsedMilliseconds}ms. \nBytes Received: {BytesReceived}\n Result: {pass}");
            await Writer.FlushAsync(CancellationToken.None);
        }

        protected abstract Task PrepareTest(CancellationToken token);
        protected abstract Task ExecuteTest(CancellationToken token);

        private TestResult EvaluateResult(long elapsedMilliseconds, bool timedOut)
        {
            if (TransportError != null)
            {
                return IsConnectionReset(TransportError) ? TestResult.ConnectionReset : TestResult.Error;
            }
            if (timedOut)
            {
                return TestResult.Timeout;
            }
            if (elapsedMilliseconds >= ThresholdMilliseconds)
            {
                return TestResult.TooSlow;
            }
            return BytesReceived < MinimumBytesReceived ? TestResult.NotEnoughData : TestResult.Success;
        }

        private static bool IsConnectionReset(Exception e)
        {
            for (var ex = e; ex != null; ex = ex.InnerException)
            {
                if (ex is SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted })
                {
                    return true;
                }
            }
            return false;
        }

        private async Task HandleException(Exception e)
        {
            await Writer.WriteLineAsync($"\nError: {e.Message}");
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
