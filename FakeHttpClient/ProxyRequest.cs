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
        Timeout,
        /// <summary>
        /// the connection closed before the whole response arrived (short of Content-Length, or no final chunk)
        /// </summary>
        Incomplete,
        /// <summary>
        /// the HTTP status code was not the one the test expected
        /// </summary>
        WrongStatus,
        /// <summary>
        /// the response body was not the length the test expected
        /// </summary>
        WrongLength,
        /// <summary>
        /// the response body's SHA-256 did not match what the test expected
        /// </summary>
        WrongContent
    }

    public class ProxyRequestFactory
    {
        /// <summary>
        /// Create the ProxyRequest to run this test.
        /// </summary>
        /// <param name="interactive">write to the console instead of a file</param>
        /// <param name="test">the URL, name, request mode, timing, and expectations</param>
        /// <param name="proxyPort"></param>
        /// <param name="proxyIp"></param>
        /// <param name="rawRequest">use the raw TCP client (required for modes, stalls, and expectations)</param>
        public static IProxyRequest CreateProxyRequest(bool interactive, TestDefinition test, int proxyPort, string proxyIp, bool rawRequest = false)
        {
            if (rawRequest)
            {
                return new TcpProxyRequest(interactive, test.Url, test.Name, proxyPort, proxyIp, test.Mode)
                {
                    Expectations = test.Expectations,
                    StartDelayMs = test.StartDelayMs ?? 0,
                    StallMs = test.StallMs ?? 0,
                };
            }
            if (test.Mode != RequestMode.Normal || test.StallMs != null || test.Expectations != TestExpectations.None)
            {
                Console.WriteLine($"WARNING: request modes, stalls, and response expectations require --raw true; ignoring them for {test.Url}");
            }
            return new HttpProxyRequest(interactive, test.Url, test.Name, proxyPort, proxyIp)
            {
                StartDelayMs = test.StartDelayMs ?? 0,
            };
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
        Task<TestOutcome> ExecuteAsync(CancellationToken token);
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

        protected long BytesReceived { get; set; }

        /// <summary>
        /// The parsed response, when the subclass sees the raw response (headers and all).
        /// </summary>
        protected HttpResponseInspector? Response { get; set; }

        public TestExpectations Expectations { get; init; } = TestExpectations.None;

        /// <summary>
        /// Wait this long before connecting (not counted in the test's time), e.g. so another test's
        /// stalled connection is accepted first.
        /// </summary>
        public int StartDelayMs { get; init; }

        /// <summary>
        /// Time the test deliberately spends doing nothing after connecting; added to the time limit.
        /// </summary>
        protected virtual int DeliberateDelayMs => 0;

        /// <summary>
        /// The request mode, for the results file.
        /// </summary>
        protected virtual string ModeName => RequestMode.Normal.ToString();

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

        public async Task<TestOutcome> ExecuteAsync(CancellationToken token)
        {
            if (StartDelayMs > 0)
            {
                try
                {
                    await Task.Delay(StartDelayMs, token);
                }
                catch (OperationCanceledException)
                {
                    // the timeout fired first; fall through and report it
                }
            }
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
            await Writer.WriteLineAsync($"\nEnd Test: Total time for {Name} ({Url}): {sw.ElapsedMilliseconds}ms. \nBytes Received: {BytesReceived}");
            if (Response is { HeadersComplete: true })
            {
                await Writer.WriteLineAsync($"Response: status {Response.StatusCode}, body {Response.BodyLength} bytes{(Response.IsComplete ? "" : " (INCOMPLETE)")}, SHA-256 {Response.BodySha256}");
            }
            await Writer.WriteLineAsync($" Result: {pass}");
            await Writer.FlushAsync(CancellationToken.None);

            var parsed = Response is { HeadersComplete: true } ? Response : null;
            return new TestOutcome(
                string.IsNullOrEmpty(Name) ? Url : Name,
                Url,
                ModeName,
                result,
                sw.ElapsedMilliseconds,
                BytesReceived,
                parsed?.StatusCode,
                parsed?.BodyLength,
                parsed?.BodySha256,
                parsed?.IsComplete,
                TransportError?.Message ?? (result == TestResult.Timeout ? "timed out waiting for the proxy" : null));
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
            if (Response is { HeadersComplete: true, IsComplete: false })
            {
                return TestResult.Incomplete;
            }
            if (Expectations.Status != null && Response?.StatusCode != Expectations.Status)
            {
                return TestResult.WrongStatus;
            }
            if (Expectations.BodyLength != null && Response?.BodyLength != Expectations.BodyLength)
            {
                return TestResult.WrongLength;
            }
            if (Expectations.Sha256 != null && !string.Equals(Response?.BodySha256, Expectations.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return TestResult.WrongContent;
            }
            if (elapsedMilliseconds >= (Expectations.MaxMs ?? ThresholdMilliseconds) + DeliberateDelayMs)
            {
                return TestResult.TooSlow;
            }
            if (!Expectations.ChecksBody && BytesReceived < MinimumBytesReceived)
            {
                return TestResult.NotEnoughData;
            }
            return TestResult.Success;
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
