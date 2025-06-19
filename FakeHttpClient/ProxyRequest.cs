using System.Diagnostics;
using System.Net;

namespace FakeHttpClient
{
    public class ProxyRequestFactory
    {
        public static IProxyRequest CreateProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp, bool rawRequest = false)
        {
            if (rawRequest)
            {
                return new TcpProxyRequest(interactive, url, name, proxyPort, proxyIp);
            }
            return new HttpProxyRequest(interactive, url, name, proxyPort, proxyIp);
        }

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
        protected const long ThresholdMilliseconds = 7 * 1000; // seconds, longer than that indicates a problem
        protected bool Interactive { get; set; }
        protected string Url { get; set; }
        protected string Name { get; set; }
        protected TextWriter Writer { get; set; }
        protected int ProxyPort { get; set; }
        protected string ProxyIp { get; set; }

        protected ProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp): this(interactive,
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

        public virtual async Task ExecuteAsync(CancellationToken token)
        {
            await PrepareTest(token);
            try
            {
                await Writer.WriteLineAsync($"Begin Test {Name}: {Url} {DateTime.Now}");
                var sw = Stopwatch.StartNew();
                await ExecuteTest(token);
                sw.Stop();
                var result = sw.ElapsedMilliseconds < ThresholdMilliseconds ? "pass!" : "FAIL (Took too long!)";
                await Writer.WriteLineAsync($"\nEnd Test: Total time for {Name} ({Url}): {sw.ElapsedMilliseconds}ms. Result: {result}");
            }
            catch (Exception e)
            {
                await HandleException(e);
                throw;
            }
        }

        protected abstract Task PrepareTest(CancellationToken token);
        protected abstract Task ExecuteTest(CancellationToken token);

        protected virtual async Task HandleException(Exception e)
        {
            await Writer.WriteLineAsync($"Error: {e.Message}");
            if (e is WebException webEx && webEx.Response != null)
            {
                using (var reader = new StreamReader(webEx.Response.GetResponseStream() ?? Stream.Null))
                {
                    await Writer.WriteLineAsync(await reader.ReadToEndAsync());
                }
            }
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            Writer?.Dispose();
        }
    }
}
