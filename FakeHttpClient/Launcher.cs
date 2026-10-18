
namespace FakeHttpClient;

public class Launcher
{
    private readonly List<TestDefinition> _tests;
    private readonly bool _interactive;
    private readonly int _proxyPort;
    private readonly string _proxyIp;
    private readonly bool _tcpProxy;

    /// <summary>
    /// Launch many tests in parallel.
    /// </summary>
    /// <remarks>For now, interactive flag is ignored (because linux/osx/windows problems)</remarks>
    /// <param name="tests"></param>
    /// <param name="interactive"></param>
    /// <param name="proxyPort"></param>
    /// <param name="proxyIp"></param>
    /// <param name="tcpProxy"></param>
    public Launcher(List<TestDefinition> tests, bool interactive, int proxyPort, string proxyIp, bool tcpProxy = false)
    {
        _tests = tests;
        _interactive = interactive;
        _proxyPort = proxyPort;
        _proxyIp = proxyIp;
        _tcpProxy = tcpProxy;
    }

    public async Task ExecuteAsync(CancellationTokenSource tokenSource)
    {
        var token = tokenSource.Token;
        // ignore interactive
        var requests = _tests
            .Select(test => ProxyRequestFactory.CreateProxyRequest(false, test, _proxyPort, _proxyIp, _tcpProxy))
            .ToList();
        try
        {
            // Each request honors the token (which carries the overall timeout) and reports its own result,
            // so waiting for all of them never hangs and every output file gets its End Test line.
            await Task.WhenAll(requests.Select(r => r.ExecuteAsync(token)));
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("Timeout reached! Remaining requests were cancelled.");
            }
        }
        finally
        {
            foreach (var r in requests)
            {
                r.Dispose();
            }
        }
    }
}
